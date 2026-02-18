using MealPlanner.Api.Features.Recipes.Models;

namespace MealPlanner.Api.Features.Recipes.Dtos;

/// <summary>
/// Shared DTO for ingredient data in requests and responses.
/// </summary>
public record IngredientDto(
	string Name,
	decimal Quantity,
	string Unit
)
{
	public Ingredient ToDomain() => new(Name, Quantity, Unit);

	public static IngredientDto FromDomain(Ingredient ingredient) =>
		new(ingredient.Name, ingredient.Quantity, ingredient.Unit);
}

/// <summary>
/// Request body for creating a new recipe (POST /api/recipes).
/// </summary>
public record CreateRecipeRequest(
	string Name,
	string Description,
	string SourceUrl,
	List<IngredientDto> Ingredients
);

/// <summary>
/// Request body for updating a recipe (PUT /api/recipes/{id}).
/// </summary>
public record UpdateRecipeRequest(
	string Name,
	string Description,
	string SourceUrl,
	List<IngredientDto> Ingredients
);

/// <summary>
/// Response body representing a recipe returned from the API.
/// </summary>
public record RecipeResponse(
	string Id,
	string Name,
	string Description,
	string SourceUrl,
	List<IngredientDto> Ingredients,
	DateTime CreatedAt,
	DateTime UpdatedAt
)
{
	public static RecipeResponse FromDomain(Recipe recipe) =>
		new(
			recipe.Id,
			recipe.Name,
			recipe.Description,
			recipe.SourceUrl,
			recipe.Ingredients.Select(IngredientDto.FromDomain).ToList(),
			recipe.CreatedAt,
			recipe.UpdatedAt
		);
}
