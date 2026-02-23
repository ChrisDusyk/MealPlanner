using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MealPlanner.Api.Features.GroceryLists.Models;

/// <summary>
/// MongoDB persistence document for a grocery list.
/// </summary>
public class GroceryListDocument
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	public string UserId { get; set; } = string.Empty;

	/// <summary>
	/// The Monday that anchors this grocery list, stored as yyyy-MM-dd.
	/// </summary>
	public string WeekStart { get; set; } = string.Empty;

	public List<GroceryListItemDocument> Items { get; set; } = [];

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// MongoDB embedded document for a single grocery list item.
/// </summary>
public class GroceryListItemDocument
{
	public string Name { get; set; } = string.Empty;

	public decimal Quantity { get; set; }

	public string Unit { get; set; } = string.Empty;

	public bool IsChecked { get; set; }

	public List<string> SourceRecipeNames { get; set; } = [];
}
