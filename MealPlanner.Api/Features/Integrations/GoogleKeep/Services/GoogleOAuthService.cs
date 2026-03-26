using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Options;
using MealPlanner.Api.Shared;
using Microsoft.Extensions.Options;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Services;

public sealed class GoogleOAuthService(
	IHttpClientFactory httpClientFactory,
	IOptions<GoogleIntegrationsOptions> options,
	IIntegrationTokenProtector tokenProtector,
	ILogger<GoogleOAuthService> logger)
	: IGoogleOAuthService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<Result<GoogleAuthorizationStart>> BuildAuthorizationUrlAsync(
		string userId,
		string redirectBaseUri,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(userId))
			return Result<GoogleAuthorizationStart>.Failure(new Error(ErrorCodes.ValidationFailed, "UserId is required."));

		if (string.IsNullOrWhiteSpace(options.Value.ClientId))
			return Result<GoogleAuthorizationStart>.Failure(new Error(
				ErrorCodes.ValidationFailed,
				"Google integration is not configured. Missing ClientId."));

		var resolvedRedirectUri = string.IsNullOrWhiteSpace(options.Value.RedirectUri)
			? redirectBaseUri?.Trim() ?? string.Empty
			: options.Value.RedirectUri.Trim();

		if (!Uri.TryCreate(resolvedRedirectUri, UriKind.Absolute, out var redirectUri))
			return Result<GoogleAuthorizationStart>.Failure(new Error(
				ErrorCodes.ValidationFailed,
				"A valid Google OAuth redirect URI is required."));

		var now = DateTime.UtcNow;
		var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
		var payload = $"{userId}|{now:O}|{nonce}";
		var state = tokenProtector.Protect(payload);
		var scopes = options.Value.KeepScopes.Count == 0
			? ["openid", "email", "https://www.googleapis.com/auth/keep"]
			: options.Value.KeepScopes;
		var encodedScopes = Uri.EscapeDataString(string.Join(" ", scopes));
		var authUrl =
			"https://accounts.google.com/o/oauth2/v2/auth"
			+ $"?client_id={Uri.EscapeDataString(options.Value.ClientId)}"
			+ $"&redirect_uri={Uri.EscapeDataString(redirectUri.ToString())}"
			+ "&response_type=code"
			+ $"&scope={encodedScopes}"
			+ "&access_type=offline"
			+ "&include_granted_scopes=true"
			+ "&prompt=consent"
			+ $"&state={Uri.EscapeDataString(state)}";

		return Result<GoogleAuthorizationStart>.Success(new GoogleAuthorizationStart(authUrl, state));
	}

	public async Task<Result<GoogleOAuthTokenSet>> ExchangeCodeAsync(
		string code,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(code))
			return Result<GoogleOAuthTokenSet>.Failure(new Error(ErrorCodes.ValidationFailed, "Authorization code is required."));

		var request = new Dictionary<string, string>
		{
			["code"] = code,
			["client_id"] = options.Value.ClientId,
			["client_secret"] = options.Value.ClientSecret,
			["redirect_uri"] = options.Value.RedirectUri,
			["grant_type"] = "authorization_code"
		};

		return await ExecuteTokenRequestAsync(request, cancellationToken);
	}

	public async Task<Result<GoogleOAuthTokenSet>> RefreshAsync(
		string refreshToken,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(refreshToken))
			return Result<GoogleOAuthTokenSet>.Failure(new Error(ErrorCodes.ValidationFailed, "Refresh token is required."));

		var request = new Dictionary<string, string>
		{
			["refresh_token"] = refreshToken,
			["client_id"] = options.Value.ClientId,
			["client_secret"] = options.Value.ClientSecret,
			["grant_type"] = "refresh_token"
		};

		return await ExecuteTokenRequestAsync(request, cancellationToken);
	}

	public async Task<Result<Unit>> RevokeTokenAsync(
		string token,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(token))
			return Result<Unit>.Failure(new Error(ErrorCodes.ValidationFailed, "Token is required for revocation."));

		try
		{
			var httpClient = httpClientFactory.CreateClient(GoogleIntegrationsOptions.OAuthHttpClientName);
			using var request = new HttpRequestMessage(
				HttpMethod.Post,
				$"https://oauth2.googleapis.com/revoke?token={Uri.EscapeDataString(token)}");
			var response = await httpClient.SendAsync(request, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				logger.LogWarning("Google token revocation failed with status {StatusCode}", (int)response.StatusCode);
				return Result<Unit>.Failure(new Error(
					ErrorCodes.ExternalServiceError,
					"Failed to revoke Google token."));
			}

			return Result<Unit>.Success(Unit.Value);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to revoke Google OAuth token.");
			return Result<Unit>.Failure(new Error(ErrorCodes.ExternalServiceError, "Failed to revoke Google token.", ex));
		}
	}

	public Result<string> ValidateStateAndGetUserId(string state)
	{
		if (string.IsNullOrWhiteSpace(state))
			return Result<string>.Failure(new Error(ErrorCodes.ValidationFailed, "State is required."));

		var unprotected = tokenProtector.Unprotect(state);
		if (!unprotected.IsSuccess)
			return Result<string>.Failure(unprotected.Error!);

		var parts = unprotected.Value!.Split('|', 3, StringSplitOptions.TrimEntries);
		if (parts.Length != 3)
			return Result<string>.Failure(new Error(ErrorCodes.ValidationFailed, "State payload is invalid."));

		if (!DateTime.TryParse(parts[1], out var issuedAtUtc))
			return Result<string>.Failure(new Error(ErrorCodes.ValidationFailed, "State issue time is invalid."));

		if (DateTime.UtcNow - issuedAtUtc.ToUniversalTime() > TimeSpan.FromMinutes(15))
			return Result<string>.Failure(new Error(ErrorCodes.ValidationFailed, "State has expired."));

		return Result<string>.Success(parts[0]);
	}

	private async Task<Result<GoogleOAuthTokenSet>> ExecuteTokenRequestAsync(
		Dictionary<string, string> requestBody,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(options.Value.ClientId)
		    || string.IsNullOrWhiteSpace(options.Value.ClientSecret))
		{
			return Result<GoogleOAuthTokenSet>.Failure(new Error(
				ErrorCodes.ValidationFailed,
				"Google integration is not configured. Missing client credentials."));
		}

		if (string.IsNullOrWhiteSpace(options.Value.RedirectUri)
		    && requestBody.TryGetValue("grant_type", out var grant)
		    && grant == "authorization_code")
		{
			return Result<GoogleOAuthTokenSet>.Failure(new Error(
				ErrorCodes.ValidationFailed,
				"Google integration is missing RedirectUri configuration."));
		}

		try
		{
			var httpClient = httpClientFactory.CreateClient(GoogleIntegrationsOptions.OAuthHttpClientName);
			using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
			{
				Content = new FormUrlEncodedContent(requestBody)
			};

			var response = await httpClient.SendAsync(request, cancellationToken);
			var content = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				logger.LogWarning("Google token exchange failed: {StatusCode} {Response}", (int)response.StatusCode, content);
				return Result<GoogleOAuthTokenSet>.Failure(new Error(
					ErrorCodes.ExternalServiceError,
					"Google token exchange failed."));
			}

			using var doc = JsonDocument.Parse(content);
			var root = doc.RootElement;
			var accessToken = root.TryGetProperty("access_token", out var accessTokenEl)
				? accessTokenEl.GetString() ?? string.Empty
				: string.Empty;
			if (string.IsNullOrWhiteSpace(accessToken))
			{
				return Result<GoogleOAuthTokenSet>.Failure(new Error(
					ErrorCodes.ExternalServiceError,
					"Google response did not include an access token."));
			}

			var refreshToken = root.TryGetProperty("refresh_token", out var refreshTokenEl)
				? Option<string>.From(refreshTokenEl.GetString())
				: Option<string>.None();
			var expiresIn = root.TryGetProperty("expires_in", out var expiresInEl)
				&& expiresInEl.TryGetInt32(out var expiresSec)
					? Option<DateTime>.Some(DateTime.UtcNow.AddSeconds(Math.Max(0, expiresSec)))
					: Option<DateTime>.None();
			var scopeText = root.TryGetProperty("scope", out var scopeEl)
				? scopeEl.GetString() ?? string.Empty
				: string.Empty;
			var scopes = scopeText
				.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.ToList();
			var idToken = root.TryGetProperty("id_token", out var idTokenEl)
				? Option<string>.From(idTokenEl.GetString())
				: Option<string>.None();

			var (subject, email) = ParseIdTokenClaims(idToken.GetValueOrNull());

			return Result<GoogleOAuthTokenSet>.Success(new GoogleOAuthTokenSet(
				AccessToken: accessToken,
				RefreshToken: refreshToken,
				ExpiresAtUtc: expiresIn,
				Scopes: scopes,
				IdToken: idToken,
				GoogleSubject: subject,
				Email: email));
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Google OAuth token operation failed.");
			return Result<GoogleOAuthTokenSet>.Failure(new Error(
				ErrorCodes.ExternalServiceError,
				"Google OAuth token operation failed.",
				ex));
		}
	}

	internal static (Option<string> Subject, Option<string> Email) ParseIdTokenClaims(string? idToken)
	{
		if (string.IsNullOrWhiteSpace(idToken))
			return (Option<string>.None(), Option<string>.None());

		try
		{
			var parts = idToken.Split('.');
			if (parts.Length < 2)
				return (Option<string>.None(), Option<string>.None());

			var payload = parts[1].Replace('-', '+').Replace('_', '/');
			var padding = 4 - payload.Length % 4;
			if (padding is > 0 and < 4)
				payload = payload.PadRight(payload.Length + padding, '=');

			var bytes = Convert.FromBase64String(payload);
			var json = Encoding.UTF8.GetString(bytes);
			var claims = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions) ?? [];
			var subject = claims.TryGetValue("sub", out var subEl)
				? Option<string>.From(subEl.GetString())
				: Option<string>.None();
			var email = claims.TryGetValue("email", out var emailEl)
				? Option<string>.From(emailEl.GetString())
				: Option<string>.None();
			return (subject, email);
		}
		catch
		{
			return (Option<string>.None(), Option<string>.None());
		}
	}
}
