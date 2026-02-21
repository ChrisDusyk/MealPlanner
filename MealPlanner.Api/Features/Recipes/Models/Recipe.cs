using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Recipes.Models;

/// <summary>
/// Domain record representing a food recipe.
/// </summary>
public record Recipe(
	string Id,
	string UserId,
	string Name,
	string Description,
	Option<string> SourceUrl,
	List<Ingredient> Ingredients,
	DateTime CreatedAt,
	DateTime UpdatedAt
);
