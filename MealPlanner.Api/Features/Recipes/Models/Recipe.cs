namespace MealPlanner.Api.Features.Recipes.Models;

/// <summary>
/// Domain record representing a food recipe.
/// </summary>
public record Recipe(
	string Id,
	string UserId,
	string Name,
	string Description,
	string SourceUrl,
	List<Ingredient> Ingredients,
	DateTime CreatedAt,
	DateTime UpdatedAt
);
