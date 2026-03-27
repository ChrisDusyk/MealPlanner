using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Models;

public record GoogleIntegrationConnection(
	string Id,
	string UserId,
	string GoogleSubject,
	IntegrationProvider Provider,
	Option<string> GoogleEmail,
	string EncryptedAccessToken,
	Option<string> EncryptedRefreshToken,
	Option<DateTime> AccessTokenExpiresAtUtc,
	IReadOnlyList<string> Scopes,
	IntegrationCapability Capability,
	DateTime CreatedAt,
	DateTime UpdatedAt,
	Option<DateTime> DisconnectedAtUtc
);
