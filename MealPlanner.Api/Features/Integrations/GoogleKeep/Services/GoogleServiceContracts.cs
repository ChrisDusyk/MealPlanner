using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Services;

public record GoogleAuthorizationStart(string AuthorizationUrl, string State);

public record GoogleOAuthTokenSet(
	string AccessToken,
	Option<string> RefreshToken,
	Option<DateTime> ExpiresAtUtc,
	IReadOnlyList<string> Scopes,
	Option<string> IdToken,
	Option<string> GoogleSubject,
	Option<string> Email
);

public record GoogleKeepUpsertResult(string ExternalItemId, bool CreatedNewItem);

public interface IGoogleOAuthService
{
	Task<Result<GoogleAuthorizationStart>> BuildAuthorizationUrlAsync(
		string userId,
		string redirectBaseUri,
		CancellationToken cancellationToken = default);

	Task<Result<GoogleOAuthTokenSet>> ExchangeCodeAsync(
		string code,
		CancellationToken cancellationToken = default);

	Task<Result<GoogleOAuthTokenSet>> RefreshAsync(
		string refreshToken,
		CancellationToken cancellationToken = default);

	Task<Result<Unit>> RevokeTokenAsync(
		string token,
		CancellationToken cancellationToken = default);

	Result<string> ValidateStateAndGetUserId(string state);
}

public interface IGoogleKeepClient
{
	Task<Result<IntegrationCapability>> GetCapabilityAsync(
		string accessToken,
		CancellationToken cancellationToken = default);

	Task<Result<GoogleKeepUpsertResult>> UpsertGroceryListAsync(
		string accessToken,
		GroceryList list,
		Option<string> existingExternalItemId,
		CancellationToken cancellationToken = default);
}

public interface IIntegrationTokenProtector
{
	string Protect(string plaintext);

	Result<string> Unprotect(string ciphertext);
}
