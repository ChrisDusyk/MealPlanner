using MealPlanner.Api.Features.Recipes.Import;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Recipes.Queries;

public record ImportIngredientsQuery(string SourceUrl) : IQuery<ImportedIngredientSet>;

public sealed class ImportIngredientsQueryHandler(IRecipeIngredientImportService importService)
	: IQueryHandler<ImportIngredientsQuery, ImportedIngredientSet>
{
	public Task<Result<ImportedIngredientSet>> HandleAsync(
		ImportIngredientsQuery query,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(query.SourceUrl))
		{
			return Task.FromResult(Result<ImportedIngredientSet>.Failure(new Error(
				ErrorCodes.ValidationFailed,
				"Source URL is required.")));
		}

		return importService.ImportFromUrlAsync(query.SourceUrl, cancellationToken);
	}
}
