namespace MealPlanner.Api.Features.Users.Dtos;

/// <summary>
/// Request payload to update per-friend auto-share preferences.
/// </summary>
public record UpdateFriendAutoSharePreferencesRequest(
	bool AutoShareMealPlans,
	bool AutoShareGroceryLists
);
