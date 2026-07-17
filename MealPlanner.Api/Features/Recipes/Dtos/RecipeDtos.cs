using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using System.ComponentModel.DataAnnotations;

namespace MealPlanner.Api.Features.Recipes.Dtos;

/// <summary>
/// Shared DTO for ingredient data in requests and responses.
/// </summary>
public record IngredientDto(
	string Name,
	decimal Quantity,
	string Unit,
	bool IsPantryStaple = false
)
{
	public Ingredient ToDomain() => new(Name, Quantity, Unit, IsPantryStaple);

	public static IngredientDto FromDomain(Ingredient ingredient) =>
		new(ingredient.Name, ingredient.Quantity, ingredient.Unit, ingredient.IsPantryStaple);
}

/// <summary>
/// Request body for creating a new recipe (POST /api/recipes).
/// </summary>
public record CreateRecipeRequest(
	string Name,
	string Description,
	string? SourceUrl,
	List<IngredientDto> Ingredients,
	[property: Range(1, int.MaxValue)] int Servings = 1
);

/// <summary>
/// Request body for updating a recipe (PUT /api/recipes/{id}).
/// </summary>
public record UpdateRecipeRequest(
	string Name,
	string Description,
	string? SourceUrl,
	List<IngredientDto> Ingredients,
	[property: Range(1, int.MaxValue)] int Servings = 1
);

/// <summary>
/// Response body representing a recipe returned from the API.
/// </summary>
public record RecipeResponse(
	string Id,
	string ContributedByUserId,
	string Name,
	string Description,
	string? SourceUrl,
	int Servings,
	List<IngredientDto> Ingredients,
	DateTime CreatedAt,
	DateTime UpdatedAt
)
{
	public static RecipeResponse FromDomain(Recipe recipe) =>
		new(
			recipe.Id,
			recipe.ContributedByUserId,
			recipe.Name,
			recipe.Description,
			recipe.SourceUrl.GetValueOrNull(),
			recipe.Servings,
			recipe.Ingredients.Select(IngredientDto.FromDomain).ToList(),
			recipe.CreatedAt,
			recipe.UpdatedAt
		);
}
