using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Users.Models;

/// <summary>
/// Domain record representing an application user linked to the identity
/// provider managed by Better Auth.
/// </summary>
public record User(
	string Id,
	string AuthUserId,
	string Name,
	Option<string> Email,
	Option<string> DisplayName,
	Option<string> Timezone,
	Option<DateTime> OnboardingCompletedAt,
	DateTime CreatedAt,
	DateTime UpdatedAt
);
