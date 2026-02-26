using MealPlanner.Api.Features.Recipes.Models;

namespace MealPlanner.Api.Features.Recipes.Import;

public record ImportedIngredientSet(
	IReadOnlyList<Ingredient> Ingredients,
	IReadOnlyList<string> Warnings
);
