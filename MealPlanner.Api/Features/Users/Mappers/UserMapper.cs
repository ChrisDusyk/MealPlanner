using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Users.Mappers;

/// <summary>
/// Centralised mapping between the <see cref="UserEntity"/> persistence shape
/// and the <see cref="User"/> domain record, so every handler in the feature
/// slice emits the same translation rules (including the onboarding and
/// profile fields introduced with the Better Auth migration).
/// </summary>
internal static class UserMapper
{
	public static User ToDomain(UserEntity entity) =>
		new(
			Id: entity.Id.ToString(),
			AuthUserId: entity.AuthUserId,
			Name: entity.Name,
			Email: Option<string>.From(entity.Email),
			DisplayName: Option<string>.From(entity.DisplayName),
			Timezone: Option<string>.From(entity.Timezone),
			OnboardingCompletedAt: entity.OnboardingCompletedAt is { } completedAt
				? Option<DateTime>.Some(completedAt)
				: Option<DateTime>.None(),
			CreatedAt: entity.CreatedAt,
			UpdatedAt: entity.UpdatedAt
		);
}
