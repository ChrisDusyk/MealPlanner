using MealPlanner.Api.Features.Recipes.Import;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Recipes.Queries;

/// <summary>
/// Represents a request to import recipe ingredients from a source URL.
/// </summary>
public record ImportIngredientsQuery(string SourceUrl) : IQuery<ImportedIngredientSet>;

/// <summary>
/// Handles ingredient import queries by delegating to the recipe import service.
/// </summary>
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
