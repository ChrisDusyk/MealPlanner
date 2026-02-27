using MealPlanner.Api.Features.Recipes.Import;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Features.Recipes.Queries;
using MealPlanner.Api.Shared;
using Moq;

namespace MealPlanner.Api.Tests.Features.Recipes.Queries;

public class ImportIngredientsQueryTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenSourceUrlMissing()
	{
		var service = new Mock<IRecipeIngredientImportService>();
		var handler = new ImportIngredientsQueryHandler(service.Object);

		var result = await handler.HandleAsync(new ImportIngredientsQuery(" "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
		service.Verify(
			s => s.ImportFromUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task HandleAsync_DelegatesToService_WhenSourceUrlPresent()
	{
		var expected = new ImportedIngredientSet(
			[new Ingredient("Flour", 2, "cups")],
			["Quantity estimated"]);

		var service = new Mock<IRecipeIngredientImportService>();
		service
			.Setup(s => s.ImportFromUrlAsync("https://example.com/recipe", It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result<ImportedIngredientSet>.Success(expected));

		var handler = new ImportIngredientsQueryHandler(service.Object);

		var result = await handler.HandleAsync(
			new ImportIngredientsQuery("https://example.com/recipe"),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Single(result.Value.Ingredients);
		Assert.Single(result.Value.Warnings);
		service.Verify(
			s => s.ImportFromUrlAsync("https://example.com/recipe", It.IsAny<CancellationToken>()),
			Times.Once);
	}
}
