using System.Security.Claims;
using MealPlanner.Api.Features.Auth;
using MealPlanner.Api.Features.FeatureFlags.Queries;
using MealPlanner.Api.Shared;
using Microsoft.Extensions.Options;

namespace MealPlanner.Api.Features.FeatureFlags;

/// <summary>
/// Endpoints that expose feature flags: the internal flagd sync source and a
/// small demo endpoint that proves the API → flagd evaluation path.
/// </summary>
public static class FeatureFlagEndpoints
{
	/// <summary>Key of the demo flag seeded by the AddFeatureFlags migration.</summary>
	public const string DemoFlagKey = "demo-banner";

	public static IEndpointRouteBuilder MapFeatureFlagEndpoints(this IEndpointRouteBuilder app)
	{
		// Internal sync source polled by flagd. Not behind the JWT policy (flagd
		// cannot present a user token); guarded by an optional shared secret and
		// intended to be reachable only over the private network.
		app.MapGet("/internal/feature-flags", GetSyncDocument)
			.WithTags("FeatureFlags")
			.ExcludeFromDescription();

		// Demo endpoint proving the API resolves flags through flagd.
		var group = app.MapGroup("/api/feature-flags")
			.WithTags("FeatureFlags")
			.RequireAuthorization(RbacAuthorization.RequireUserRolePolicy);

		group.MapGet("/demo", GetDemoFlag);

		return app;
	}

	private static async Task<IResult> GetSyncDocument(
		HttpContext httpContext,
		IQueryHandler<GetFeatureFlagDocumentQuery, string> handler,
		IOptions<FeatureFlagsOptions> options,
		CancellationToken cancellationToken)
	{
		var expectedToken = options.Value.SyncToken;
		if (!string.IsNullOrWhiteSpace(expectedToken))
		{
			// flagd's HTTP sync source only supports authenticating via its
			// `authHeader` config field, which sets the literal Authorization
			// header value (bearer/basic style) — it cannot send an arbitrary
			// custom header. So the shared secret travels as a bearer token.
			var providedAuthorization = httpContext.Request.Headers.Authorization.ToString();
			var expectedAuthorization = $"Bearer {expectedToken}";
			if (!CryptographicEquals(providedAuthorization, expectedAuthorization))
			{
				return Results.Unauthorized();
			}
		}

		var result = await handler.HandleAsync(new GetFeatureFlagDocumentQuery(), cancellationToken);
		return result.Match(
			onSuccess: document => Results.Content(document, "application/json"),
			onFailure: error => Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> GetDemoFlag(
		HttpContext httpContext,
		IFeatureFlagClient featureFlags,
		CancellationToken cancellationToken)
	{
		var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
			?? httpContext.User.FindFirst("sub")?.Value;

		var enabled = await featureFlags.GetBooleanValueAsync(
			DemoFlagKey,
			defaultValue: false,
			targetingKey: userId,
			cancellationToken: cancellationToken);

		return Results.Ok(new { flag = DemoFlagKey, enabled });
	}

	/// <summary>
	/// Constant-time string comparison. Both inputs are hashed to a fixed length
	/// first so the comparison time does not depend on the provided token's
	/// length (a raw <c>FixedTimeEquals</c> short-circuits on length mismatch).
	/// </summary>
	private static bool CryptographicEquals(string a, string b)
	{
		var aHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(a));
		var bHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(b));
		return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(aHash, bHash);
	}
}
