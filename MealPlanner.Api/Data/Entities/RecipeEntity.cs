namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for a recipe.
/// </summary>
public class RecipeEntity
{
	public Guid Id { get; set; }

	public Guid FamilyGroupId { get; set; }

	/// <summary>
	/// Auth user id (JWT sub) of the family member who created this recipe or
	/// brought it into the family. Used to copy recipes back to a member when
	/// they leave the family.
	/// </summary>
	public string ContributedByUserId { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string Description { get; set; } = string.Empty;

	public int Servings { get; set; } = 1;

	public string? SourceUrl { get; set; }

	/// <summary>
	/// When this recipe was added by copying from the admin-curated catalogue,
	/// the source catalogue recipe ID. Used to prevent duplicate adds.
	/// </summary>
	public Guid? CatalogueRecipeId { get; set; }

	/// <summary>
	/// Stored as jsonb in PostgreSQL.
	/// </summary>
	public List<IngredientData> Ingredients { get; set; } = [];

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Embedded ingredient data stored as part of the recipe jsonb column.
/// </summary>
public class IngredientData
{
	public string Name { get; set; } = string.Empty;

	public decimal Quantity { get; set; }

	public string Unit { get; set; } = string.Empty;

	public bool IsPantryStaple { get; set; }
}
