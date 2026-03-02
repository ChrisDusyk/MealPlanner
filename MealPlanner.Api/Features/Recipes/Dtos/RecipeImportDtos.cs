namespace MealPlanner.Api.Features.Recipes.Dtos;

public record ImportIngredientsRequest(string SourceUrl);

public record ImportIngredientsResponse(
	List<IngredientDto> Ingredients,
	List<string> Warnings,
	string? RecipeName = null,
	int? Servings = null
);
