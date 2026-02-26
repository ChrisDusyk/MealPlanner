using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Recipes.Import;

public interface IRecipePageTextExtractor
{
	Task<Result<string>> ExtractAsync(string sourceUrl, CancellationToken cancellationToken = default);
}
