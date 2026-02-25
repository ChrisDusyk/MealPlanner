using MealPlanner.Api.Features.MealPlans.Models;

namespace MealPlanner.Api.Features.GroceryLists.Models;

/// <summary>
/// Domain record representing a grocery list share between two users for a specific week.
/// </summary>
public record GroceryListShare(
	string Id,
	string OwnerUserId,
	string SharedWithUserId,
	string WeekStart,
	SharePermission Permission,
	DateTime SharedAt,
	bool DismissedByRecipient,
	string SharedWithName = "",
	string SharedWithEmail = ""
);
