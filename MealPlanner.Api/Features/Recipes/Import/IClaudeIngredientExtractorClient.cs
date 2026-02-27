using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Recipes.Import;

public interface IClaudeIngredientExtractorClient
{
	Task<Result<ImportedIngredientSet>> ExtractIngredientsAsync(string recipeText, CancellationToken cancellationToken = default);
}
