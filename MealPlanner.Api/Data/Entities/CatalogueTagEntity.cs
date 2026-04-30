namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for a curated tag that can be applied to catalogue recipes.
/// </summary>
public class CatalogueTagEntity
{
	public Guid Id { get; set; }

	public string Slug { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; }

	public List<CatalogueRecipeTagEntity> RecipeTags { get; set; } = [];
}
