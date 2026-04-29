using MealPlanner.Api.Features.Users.Models;

namespace MealPlanner.Api.Features.Users.Dtos;

/// <summary>
/// API response payload for user data.
/// </summary>
public record UserResponse(
	string Id,
	string AuthUserId,
	string Name,
	string? Email,
	string? DisplayName,
	string? Timezone,
	DateTime? OnboardingCompletedAt,
	DateTime CreatedAt,
	DateTime UpdatedAt)
{
	public static UserResponse FromDomain(User user) =>
		new(
			Id: user.Id,
			AuthUserId: user.AuthUserId,
			Name: user.Name,
			Email: user.Email.GetValueOrNull(),
			DisplayName: user.DisplayName.GetValueOrNull(),
			Timezone: user.Timezone.GetValueOrNull(),
			OnboardingCompletedAt: user.OnboardingCompletedAt.Match<DateTime?>(
				onSome: value => value,
				onNone: () => null),
			CreatedAt: user.CreatedAt,
			UpdatedAt: user.UpdatedAt
		);
}
