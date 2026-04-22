namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for a grocery list.
/// </summary>
public class GroceryListEntity
{
	public Guid Id { get; set; }

	public string UserId { get; set; } = string.Empty;

	/// <summary>
	/// The Monday that anchors this grocery list, stored as yyyy-MM-dd.
	/// </summary>
	public string WeekStart { get; set; } = string.Empty;

	/// <summary>
	/// Stored as jsonb in PostgreSQL.
	/// </summary>
	public List<GroceryListItemData> Items { get; set; } = [];

	/// <summary>
	/// Stored as jsonb in PostgreSQL.
	/// </summary>
	public List<GroceryListItemData> PantryStapleItems { get; set; } = [];

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Embedded grocery list item data stored as part of the grocery list jsonb columns.
/// </summary>
public class GroceryListItemData
{
	public string Name { get; set; } = string.Empty;

	public decimal Quantity { get; set; }

	public string Unit { get; set; } = string.Empty;

	public bool IsChecked { get; set; }

	public List<string> SourceRecipeNames { get; set; } = [];
}
