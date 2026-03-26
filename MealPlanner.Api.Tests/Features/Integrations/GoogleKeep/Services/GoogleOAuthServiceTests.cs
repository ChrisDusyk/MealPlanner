using System.Text;
using System.Text.Json;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Options;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Services;
using MealPlanner.Api.Shared;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace MealPlanner.Api.Tests.Features.Integrations.GoogleKeep.Services;

public class GoogleOAuthServiceTests
{
	[Fact]
	public async Task BuildAuthorizationUrlAsync_AllowsAuth0StyleUserIdsInStateRoundTrip()
	{
		var service = CreateService(new GoogleIntegrationsOptions
		{
			ClientId = "client-1",
			ClientSecret = "secret-1",
			RedirectUri = "https://localhost/callback"
		});

		var start = await service.BuildAuthorizationUrlAsync(
			"auth0|user-123",
			"https://ignored.example.com/callback",
			TestContext.Current.CancellationToken);

		Assert.True(start.IsSuccess);
		var stateResult = service.ValidateStateAndGetUserId(start.Value!.State);
		Assert.True(stateResult.IsSuccess);
		Assert.Equal("auth0|user-123", stateResult.Value);
	}

	[Fact]
	public async Task BuildAuthorizationUrlAsync_ReturnsValidationFailure_WhenRedirectUriNotConfigured()
	{
		var service = CreateService(new GoogleIntegrationsOptions
		{
			ClientId = "client-1",
			ClientSecret = "secret-1",
			RedirectUri = ""
		});

		var result = await service.BuildAuthorizationUrlAsync(
			"auth0|user-123",
			"https://caller-provided.example.com/callback",
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public void ParseIdTokenClaims_ReturnsSubjectAndEmail_WhenClaimsExist()
	{
		var payload = JsonSerializer.Serialize(new { sub = "google-subject-1", email = "user@example.com" });
		var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
			.TrimEnd('=')
			.Replace('+', '-')
			.Replace('/', '_');
		var token = $"header.{payloadBase64}.signature";

		var (subject, email) = GoogleOAuthService.ParseIdTokenClaims(token);

		Assert.True(subject.HasValue);
		Assert.Equal("google-subject-1", subject.Value);
		Assert.True(email.HasValue);
		Assert.Equal("user@example.com", email.Value);
	}

	[Fact]
	public void ParseIdTokenClaims_ReturnsNone_WhenTokenMalformed()
	{
		var (subject, email) = GoogleOAuthService.ParseIdTokenClaims("not-a-jwt");

		Assert.False(subject.HasValue);
		Assert.False(email.HasValue);
	}

	private static GoogleOAuthService CreateService(GoogleIntegrationsOptions options)
	{
		var protector = new IntegrationTokenProtector(
			DataProtectionProvider.Create("MealPlanner.Api.Tests"),
			Options.Create(options));
		return new GoogleOAuthService(
			new Mock<IHttpClientFactory>().Object,
			Options.Create(options),
			protector,
			NullLogger<GoogleOAuthService>.Instance);
	}
}
