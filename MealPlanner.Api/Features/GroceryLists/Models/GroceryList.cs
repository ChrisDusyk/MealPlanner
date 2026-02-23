namespace MealPlanner.Api.Features.GroceryLists.Models;

/// <summary>
/// A single item on a grocery list — an aggregated ingredient or a custom entry.
/// SourceRecipeNames tracks which recipes contributed this item (empty for custom items).
/// </summary>
public record GroceryListItem(
	string Name,
	decimal Quantity,
	string Unit,
	bool IsChecked,
	List<string> SourceRecipeNames
);

/// <summary>
/// A grocery list generated from a weekly meal plan, tied to a user and week.
/// </summary>
public record GroceryList(
	string Id,
	string UserId,
	DateOnly WeekStart,
	List<GroceryListItem> Items,
	DateTime CreatedAt,
	DateTime UpdatedAt
);
