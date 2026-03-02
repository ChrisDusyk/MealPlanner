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
	List<GroceryListItemDto> PantryStapleItems,
	DateTime CreatedAt,
	DateTime UpdatedAt
)
{
	public static GroceryListResponse FromDomain(GroceryList list) =>
		new(
			list.Id,
			list.WeekStart.ToString("yyyy-MM-dd"),
			list.Items.Select(GroceryListItemDto.FromDomain).ToList(),
			list.PantryStapleItems.Select(GroceryListItemDto.FromDomain).ToList(),
			list.CreatedAt,
			list.UpdatedAt
		);
}

/// <summary>
/// Request body for adding a custom item to an existing grocery list.
/// </summary>
public record AddCustomItemRequest(string Name);

// ── Sharing DTOs ──────────────────────────────────────

/// <summary>
/// Request body for sharing a grocery list with another user.
/// </summary>
public record ShareGroceryListRequest(
	string Email,
	string WeekStart,
	string Permission
);

/// <summary>
/// Response body representing a single grocery list share record.
/// Includes enriched recipient info when available.
/// </summary>
public record GroceryListShareResponse(
	string Id,
	string OwnerUserId,
	string SharedWithUserId,
	string SharedWithName,
	string SharedWithEmail,
	string WeekStart,
	string Permission,
	DateTime SharedAt
)
{
	public static GroceryListShareResponse FromDomain(
		GroceryListShare share,
		string sharedWithName = "",
		string sharedWithEmail = "") =>
		new(
			Id: share.Id,
			OwnerUserId: share.OwnerUserId,
			SharedWithUserId: share.SharedWithUserId,
			SharedWithName: string.IsNullOrWhiteSpace(sharedWithName) ? share.SharedWithName : sharedWithName,
			SharedWithEmail: string.IsNullOrWhiteSpace(sharedWithEmail) ? share.SharedWithEmail : sharedWithEmail,
			WeekStart: share.WeekStart,
			Permission: share.Permission.ToString(),
			SharedAt: share.SharedAt
		);
}

/// <summary>
/// A shared grocery list as seen by the recipient, including full list data and owner info.
/// </summary>
public record SharedGroceryListResponse(
	string ShareId,
	string OwnerUserId,
	string OwnerName,
	string OwnerEmail,
	string Permission,
	GroceryListResponse GroceryList
);
