using MealPlanner.Api.Features.Users.Queries;

namespace MealPlanner.Api.Features.Users.Dtos;

/// <summary>
/// Response payload for a friend list item.
/// </summary>
public record FriendSummaryResponse(
	string UserId,
	string Name,
	string? Email,
	bool AutoShareMealPlans,
	bool AutoShareGroceryLists
)
{
	public static FriendSummaryResponse FromDomain(FriendSummary item) =>
		new(
			UserId: item.UserId,
			Name: item.Name,
			Email: item.Email,
			AutoShareMealPlans: item.AutoShareMealPlans,
			AutoShareGroceryLists: item.AutoShareGroceryLists
		);
}
