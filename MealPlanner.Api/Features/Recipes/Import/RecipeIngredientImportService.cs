using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Recipes.Import;

public sealed class RecipeIngredientImportService(
	IRecipePageTextExtractor recipePageTextExtractor,
	IClaudeIngredientExtractorClient claudeIngredientExtractorClient)
	: IRecipeIngredientImportService
{
	public async Task<Result<ImportedIngredientSet>> ImportFromUrlAsync(
		string sourceUrl,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(sourceUrl))
		{
			return Result<ImportedIngredientSet>.Failure(new Error(
				ErrorCodes.ValidationFailed,
				"Source URL is required."));
		}

		var extractedTextResult = await recipePageTextExtractor.ExtractAsync(sourceUrl, cancellationToken);
		if (!extractedTextResult.IsSuccess)
		{
			return Result<ImportedIngredientSet>.Failure(extractedTextResult.Error!);
		}

		var ingredientResult = await claudeIngredientExtractorClient.ExtractIngredientsAsync(
			extractedTextResult.Value!,
			cancellationToken);

		if (!ingredientResult.IsSuccess)
		{
			return Result<ImportedIngredientSet>.Failure(ingredientResult.Error!);
		}

		return Result<ImportedIngredientSet>.Success(ingredientResult.Value!);
	}
}
