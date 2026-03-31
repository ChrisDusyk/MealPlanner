using MealPlanner.Api.Features.Recipes.Commands;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.Recipes.Commands;

public class CreateRecipeTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenNameIsMissing()
	{
		var handler = new CreateRecipeCommandHandler(TestDbContextFactory.CreateContext());
		var command = new CreateRecipeCommand("u1", " ", "desc", 1, Option<string>.None(), []);

		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_CreatesRecipe_WhenCommandValid()
	{
		var handler = new CreateRecipeCommandHandler(TestDbContextFactory.CreateContext());
		var command = new CreateRecipeCommand(
			"u1",
			"Chili",
			"Spicy",
			4,
			Option<string>.Some("https://example.com/chili"),
			[new Ingredient("Beans", 2, "cups"), new Ingredient("Salt", 1, "tsp", true)]);

		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal("u1", result.Value!.UserId);
		Assert.Equal("Chili", result.Value.Name);
		Assert.True(result.Value.SourceUrl.HasValue);
		Assert.Equal(2, result.Value.Ingredients.Count);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseFailure_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new CreateRecipeCommandHandler(context);
		var command = new CreateRecipeCommand("u1", "Chili", "Spicy", 1, Option<string>.None(), []);
		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
