namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for an admin-curated catalogue recipe that any user can copy
/// into their own recipe collection.
/// </summary>
public class CatalogueRecipeEntity
{
	public Guid Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public string Description { get; set; } = string.Empty;

	public int Servings { get; set; } = 1;

	public string? SourceUrl { get; set; }

	public string? ImageUrl { get; set; }

	/// <summary>
	/// Stored as jsonb in PostgreSQL. Reuses the user-recipe ingredient shape.
	/// </summary>
	public List<IngredientData> Ingredients { get; set; } = [];

	public bool IsPublished { get; set; }

	public DateTime? PublishedAt { get; set; }

	/// <summary>
	/// Number of times this catalogue recipe has been added to a user's
	/// personal collection. Used for the "popular" sort.
	/// </summary>
	public int AddCount { get; set; }

	public string CreatedByUserId { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }

	public List<CatalogueRecipeTagEntity> Tags { get; set; } = [];
}
