using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MealPlanner.Api.Features.Recipes.Models;

/// <summary>
/// MongoDB persistence document for a recipe.
/// </summary>
public class RecipeDocument
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	public string UserId { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string Description { get; set; } = string.Empty;

	public string? SourceUrl { get; set; }

	public List<IngredientDocument> Ingredients { get; set; } = [];

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// MongoDB embedded document for an ingredient.
/// </summary>
public class IngredientDocument
{
	public string Name { get; set; } = string.Empty;

	public decimal Quantity { get; set; }

	public string Unit { get; set; } = string.Empty;
}
