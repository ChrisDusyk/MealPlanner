using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Catalogue.Models;

/// <summary>
/// Domain record representing a curated tag for catalogue recipes.
/// </summary>
public record CatalogueTag(
	Guid Id,
	string Slug,
	string Name,
	DateTime CreatedAt
);

/// <summary>
/// Domain record representing an admin-curated catalogue recipe.
/// </summary>
public record CatalogueRecipe(
	Guid Id,
	string Name,
	string Description,
	int Servings,
	Option<string> SourceUrl,
	Option<string> ImageUrl,
	List<Ingredient> Ingredients,
	bool IsPublished,
	Option<DateTime> PublishedAt,
	int AddCount,
	string CreatedByUserId,
	DateTime CreatedAt,
	DateTime UpdatedAt,
	List<CatalogueTag> Tags
);

/// <summary>
/// Lightweight projection used in browse/list views. Includes the
/// "alreadyAdded" flag computed against the current user's recipes.
/// </summary>
public record CatalogueRecipeListItem(
	Guid Id,
	string Name,
	string Description,
	Option<string> ImageUrl,
	int Servings,
	int IngredientCount,
	int AddCount,
	bool IsPublished,
	Option<DateTime> PublishedAt,
	List<CatalogueTag> Tags,
	bool AlreadyAdded,
	DateTime UpdatedAt
);
