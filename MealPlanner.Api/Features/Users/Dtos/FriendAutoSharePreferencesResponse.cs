using MealPlanner.Api.Features.Users.Commands;

namespace MealPlanner.Api.Features.Users.Dtos;

/// <summary>
/// Response payload for per-friend auto-share preferences.
/// </summary>
public record FriendAutoSharePreferencesResponse(
	bool AutoShareMealPlans,
	bool AutoShareGroceryLists
)
{
	public static FriendAutoSharePreferencesResponse FromDomain(UpdateFriendAutoSharePreferencesResult result) =>
		new(result.AutoShareMealPlans, result.AutoShareGroceryLists);
}
