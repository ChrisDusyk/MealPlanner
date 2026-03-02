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
			["Quantity estimated"],
			Option<string>.Some("Test Recipe"),
			Option<int>.Some(4));

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
		Assert.True(result.Value.RecipeName.HasValue);
		Assert.Equal("Test Recipe", result.Value.RecipeName.Value);
		Assert.True(result.Value.Servings.HasValue);
		Assert.Equal(4, result.Value.Servings.Value);
		service.Verify(
			s => s.ImportFromUrlAsync("https://example.com/recipe", It.IsAny<CancellationToken>()),
			Times.Once);
	}
}
