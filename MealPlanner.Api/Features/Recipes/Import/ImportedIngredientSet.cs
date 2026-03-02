using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Recipes.Import;

public record ImportedIngredientSet(
	IReadOnlyList<Ingredient> Ingredients,
	IReadOnlyList<string> Warnings,
	Option<string> RecipeName = default,
	Option<int> Servings = default
);
