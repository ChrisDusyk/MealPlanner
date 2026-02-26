using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Recipes.Import;

public interface IRecipeIngredientImportService
{
	Task<Result<ImportedIngredientSet>> ImportFromUrlAsync(string sourceUrl, CancellationToken cancellationToken = default);
}
