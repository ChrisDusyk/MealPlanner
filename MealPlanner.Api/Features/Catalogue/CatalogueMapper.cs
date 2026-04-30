using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Catalogue.Models;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Catalogue;

/// <summary>
/// Shared entity ↔ domain mappers for catalogue recipes and tags.
/// </summary>
internal static class CatalogueMapper
{
	internal static CatalogueTag ToDomain(CatalogueTagEntity entity) =>
		new(entity.Id, entity.Slug, entity.Name, entity.CreatedAt);

	internal static CatalogueRecipe ToDomain(CatalogueRecipeEntity entity)
	{
		var tags = entity.Tags
			.Where(t => t.Tag is not null)
			.Select(t => ToDomain(t.Tag))
			.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
			.ToList();

		return new CatalogueRecipe(
			Id: entity.Id,
			Name: entity.Name,
			Description: entity.Description,
			Servings: entity.Servings,
			SourceUrl: Option<string>.From(entity.SourceUrl),
			ImageUrl: Option<string>.From(entity.ImageUrl),
			Ingredients: entity.Ingredients
				.Select(i => new Ingredient(i.Name, i.Quantity, i.Unit, i.IsPantryStaple))
				.ToList(),
			IsPublished: entity.IsPublished,
			PublishedAt: entity.PublishedAt.HasValue
				? Option<DateTime>.Some(entity.PublishedAt.Value)
				: Option<DateTime>.None(),
			AddCount: entity.AddCount,
			CreatedByUserId: entity.CreatedByUserId,
			CreatedAt: entity.CreatedAt,
			UpdatedAt: entity.UpdatedAt,
			Tags: tags);
	}

	internal static CatalogueRecipeListItem ToListItem(CatalogueRecipeEntity entity, bool alreadyAdded)
	{
		var tags = entity.Tags
			.Where(t => t.Tag is not null)
			.Select(t => ToDomain(t.Tag))
			.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
			.ToList();

		return new CatalogueRecipeListItem(
			Id: entity.Id,
			Name: entity.Name,
			Description: entity.Description,
			ImageUrl: Option<string>.From(entity.ImageUrl),
			Servings: entity.Servings,
			IngredientCount: entity.Ingredients.Count,
			AddCount: entity.AddCount,
			IsPublished: entity.IsPublished,
			PublishedAt: entity.PublishedAt.HasValue
				? Option<DateTime>.Some(entity.PublishedAt.Value)
				: Option<DateTime>.None(),
			Tags: tags,
			AlreadyAdded: alreadyAdded,
			UpdatedAt: entity.UpdatedAt);
	}

	internal static List<IngredientData> ToIngredientData(IEnumerable<Ingredient> ingredients) =>
		ingredients.Select(i => new IngredientData
		{
			Name = i.Name,
			Quantity = i.Quantity,
			Unit = i.Unit,
			IsPantryStaple = i.IsPantryStaple
		}).ToList();
}
