using MealPlanner.Api.Features.Recipes.Import;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using Moq;

namespace MealPlanner.Api.Tests.Features.Recipes.Import;

public class RecipeIngredientImportServiceTests
{
	[Fact]
	public async Task ImportFromUrlAsync_ReturnsValidationFailure_WhenSourceUrlMissing()
	{
		var textExtractor = new Mock<IRecipePageTextExtractor>();
		var ingredientExtractor = new Mock<IClaudeIngredientExtractorClient>();
		var service = new RecipeIngredientImportService(textExtractor.Object, ingredientExtractor.Object);

		var result = await service.ImportFromUrlAsync(" ", TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
		textExtractor.Verify(
			x => x.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
			Times.Never);
		ingredientExtractor.Verify(
			x => x.ExtractIngredientsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task ImportFromUrlAsync_ReturnsExtractorError_WhenPageTextExtractionFails()
	{
		var extractionError = new Error(ErrorCodes.ExternalServiceError, "Page fetch failed.");
		var textExtractor = new Mock<IRecipePageTextExtractor>();
		textExtractor
			.Setup(x => x.ExtractAsync("https://example.com/recipe", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result<string>.Failure(extractionError));

		var ingredientExtractor = new Mock<IClaudeIngredientExtractorClient>();
		var service = new RecipeIngredientImportService(textExtractor.Object, ingredientExtractor.Object);

		var result = await service.ImportFromUrlAsync("https://example.com/recipe", TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Same(extractionError, result.Error);
		ingredientExtractor.Verify(
			x => x.ExtractIngredientsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task ImportFromUrlAsync_ReturnsAiError_WhenIngredientExtractionFails()
	{
		var aiError = new Error(ErrorCodes.ExternalServiceError, "AI import failed.");
		var textExtractor = new Mock<IRecipePageTextExtractor>();
		textExtractor
			.Setup(x => x.ExtractAsync("https://example.com/recipe", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result<string>.Success("2 cups flour"));

		var ingredientExtractor = new Mock<IClaudeIngredientExtractorClient>();
		ingredientExtractor
			.Setup(x => x.ExtractIngredientsAsync("2 cups flour", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result<ImportedIngredientSet>.Failure(aiError));

		var service = new RecipeIngredientImportService(textExtractor.Object, ingredientExtractor.Object);

		var result = await service.ImportFromUrlAsync("https://example.com/recipe", TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Same(aiError, result.Error);
	}

	[Fact]
	public async Task ImportFromUrlAsync_ReturnsImportedIngredients_WhenDependenciesSucceed()
	{
		var expected = new ImportedIngredientSet(
			[new Ingredient("Flour", 2m, "cups")],
			["Quantity inferred"],
			Option<string>.Some("Banana Bread"),
			Option<int>.Some(8));

		var textExtractor = new Mock<IRecipePageTextExtractor>();
		textExtractor
			.Setup(x => x.ExtractAsync("https://example.com/recipe", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result<string>.Success("2 cups flour"));

		var ingredientExtractor = new Mock<IClaudeIngredientExtractorClient>();
		ingredientExtractor
			.Setup(x => x.ExtractIngredientsAsync("2 cups flour", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result<ImportedIngredientSet>.Success(expected));

		var service = new RecipeIngredientImportService(textExtractor.Object, ingredientExtractor.Object);

		var result = await service.ImportFromUrlAsync("https://example.com/recipe", TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Single(result.Value.Ingredients);
		Assert.Single(result.Value.Warnings);
		Assert.Equal("Flour", result.Value.Ingredients[0].Name);
		Assert.True(result.Value.RecipeName.HasValue);
		Assert.Equal("Banana Bread", result.Value.RecipeName.Value);
		Assert.True(result.Value.Servings.HasValue);
		Assert.Equal(8, result.Value.Servings.Value);
	}
}
