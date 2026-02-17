namespace MealPlanner.Api.Features.Recipes.Models;

/// <summary>
/// Domain value object representing an ingredient in a recipe.
/// </summary>
public record Ingredient(
	string Name,
	decimal Quantity,
	string Unit
);
