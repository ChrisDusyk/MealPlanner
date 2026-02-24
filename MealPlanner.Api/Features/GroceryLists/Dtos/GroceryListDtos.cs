using MealPlanner.Api.Features.GroceryLists.Models;

namespace MealPlanner.Api.Features.GroceryLists.Dtos;

/// <summary>
/// DTO for a single grocery list item.
/// </summary>
public record GroceryListItemDto(
	string Name,
	decimal Quantity,
	string Unit,
	bool IsChecked,
	List<string> SourceRecipeNames
)
{
	public static GroceryListItemDto FromDomain(GroceryListItem item) =>
		new(item.Name, item.Quantity, item.Unit, item.IsChecked, item.SourceRecipeNames);
}

/// <summary>
/// Response body representing a grocery list.
/// </summary>
public record GroceryListResponse(
	string Id,
	string WeekStart,
	List<GroceryListItemDto> Items,
	DateTime CreatedAt,
	DateTime UpdatedAt
)
{
	public static GroceryListResponse FromDomain(GroceryList list) =>
		new(
			list.Id,
			list.WeekStart.ToString("yyyy-MM-dd"),
			list.Items.Select(GroceryListItemDto.FromDomain).ToList(),
			list.CreatedAt,
			list.UpdatedAt
		);
}

/// <summary>
/// Request body for adding a custom item to an existing grocery list.
/// </summary>
public record AddCustomItemRequest(string Name);
