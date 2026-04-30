using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Catalogue.Models;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using System.ComponentModel.DataAnnotations;

namespace MealPlanner.Api.Features.Catalogue.Dtos;

/// <summary>
/// Tag DTO used in both admin and user-facing responses.
/// </summary>
public record CatalogueTagDto(Guid Id, string Slug, string Name)
{
	public static CatalogueTagDto FromDomain(CatalogueTag tag) =>
		new(tag.Id, tag.Slug, tag.Name);

	internal static CatalogueTagDto FromEntity(CatalogueTagEntity entity) =>
		new(entity.Id, entity.Slug, entity.Name);
}

/// <summary>
/// Ingredient DTO for catalogue recipes (mirrors the user-recipe shape).
/// </summary>
public record CatalogueIngredientDto(
	string Name,
	decimal Quantity,
	string Unit,
	bool IsPantryStaple = false
)
{
	public Ingredient ToDomain() => new(Name, Quantity, Unit, IsPantryStaple);

	public static CatalogueIngredientDto FromDomain(Ingredient ingredient) =>
		new(ingredient.Name, ingredient.Quantity, ingredient.Unit, ingredient.IsPantryStaple);
}

/// <summary>
/// Full catalogue recipe response (used in detail views and admin list).
/// </summary>
public record CatalogueRecipeResponse(
	Guid Id,
	string Name,
	string Description,
	int Servings,
	string? SourceUrl,
	string? ImageUrl,
	List<CatalogueIngredientDto> Ingredients,
	bool IsPublished,
	DateTime? PublishedAt,
	int AddCount,
	List<CatalogueTagDto> Tags,
	bool AlreadyAdded,
	DateTime CreatedAt,
	DateTime UpdatedAt
)
{
	public static CatalogueRecipeResponse FromDomain(CatalogueRecipe recipe, bool alreadyAdded) =>
		new(
			recipe.Id,
			recipe.Name,
			recipe.Description,
			recipe.Servings,
			recipe.SourceUrl.GetValueOrNull(),
			recipe.ImageUrl.GetValueOrNull(),
			recipe.Ingredients.Select(CatalogueIngredientDto.FromDomain).ToList(),
			recipe.IsPublished,
			recipe.PublishedAt.Match(d => (DateTime?)d, () => null),
			recipe.AddCount,
			recipe.Tags.Select(CatalogueTagDto.FromDomain).ToList(),
			alreadyAdded,
			recipe.CreatedAt,
			recipe.UpdatedAt
		);
}

/// <summary>
/// Lightweight list/browse projection.
/// </summary>
public record CatalogueRecipeListItemResponse(
	Guid Id,
	string Name,
	string Description,
	string? ImageUrl,
	int Servings,
	int IngredientCount,
	int AddCount,
	bool IsPublished,
	DateTime? PublishedAt,
	List<CatalogueTagDto> Tags,
	bool AlreadyAdded,
	DateTime UpdatedAt
)
{
	public static CatalogueRecipeListItemResponse FromDomain(CatalogueRecipeListItem item) =>
		new(
			item.Id,
			item.Name,
			item.Description,
			item.ImageUrl.GetValueOrNull(),
			item.Servings,
			item.IngredientCount,
			item.AddCount,
			item.IsPublished,
			item.PublishedAt.Match(d => (DateTime?)d, () => null),
			item.Tags.Select(CatalogueTagDto.FromDomain).ToList(),
			item.AlreadyAdded,
			item.UpdatedAt
		);
}

/// <summary>
/// Request body for creating a catalogue recipe (admin only).
/// </summary>
public record CreateCatalogueRecipeRequest(
	[property: Required] string Name,
	string Description,
	string? SourceUrl,
	string? ImageUrl,
	List<CatalogueIngredientDto> Ingredients,
	List<Guid> TagIds,
	[property: Range(1, int.MaxValue)] int Servings = 1,
	bool IsPublished = false
);

/// <summary>
/// Request body for updating a catalogue recipe (admin only).
/// </summary>
public record UpdateCatalogueRecipeRequest(
	[property: Required] string Name,
	string Description,
	string? SourceUrl,
	string? ImageUrl,
	List<CatalogueIngredientDto> Ingredients,
	List<Guid> TagIds,
	[property: Range(1, int.MaxValue)] int Servings = 1
);

/// <summary>
/// Request body for toggling published state.
/// </summary>
public record SetCatalogueRecipePublishedRequest(bool IsPublished);

/// <summary>
/// Request body for creating/updating a curated tag.
/// </summary>
public record CatalogueTagRequest(
	[property: Required, MaxLength(64)] string Name,
	[property: Required, MaxLength(64)] string Slug
);
