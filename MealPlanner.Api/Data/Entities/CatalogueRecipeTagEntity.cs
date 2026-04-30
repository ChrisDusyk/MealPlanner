namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core join entity linking catalogue recipes to curated tags.
/// </summary>
public class CatalogueRecipeTagEntity
{
	public Guid CatalogueRecipeId { get; set; }

	public CatalogueRecipeEntity CatalogueRecipe { get; set; } = null!;

	public Guid TagId { get; set; }

	public CatalogueTagEntity Tag { get; set; } = null!;
}
