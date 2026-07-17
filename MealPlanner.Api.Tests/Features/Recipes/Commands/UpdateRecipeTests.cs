using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Recipes.Commands;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.Recipes.Commands;

public class UpdateRecipeTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenNameIsMissing()
	{
		var handler = new UpdateRecipeCommandHandler(TestDbContextFactory.CreateContext());
		var command = new UpdateRecipeCommand(Guid.NewGuid().ToString(), TestIds.Family("u1"), " ", "desc", 1, Option<string>.None(), []);

		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenRecipeMissing()
	{
		var handler = new UpdateRecipeCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new UpdateRecipeCommand(Guid.NewGuid().ToString(), TestIds.Family("u1"), "Updated", "desc", 1, Option<string>.None(), []),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsUnauthorized_WhenRecipeBelongsToAnotherUser()
	{
		var id = Guid.NewGuid();
		var now = DateTime.UtcNow;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Recipes.Add(new RecipeEntity
			{
				Id = id,
				FamilyGroupId = TestIds.Family("u2"),
				ContributedByUserId = "u2",
				Name = "Old",
				Description = "old",
				Servings = 1,
				Ingredients = [],
				CreatedAt = now,
				UpdatedAt = now
			});
		});

		var handler = new UpdateRecipeCommandHandler(context);
		var result = await handler.HandleAsync(
			new UpdateRecipeCommand(id.ToString(), TestIds.Family("u1"), "Updated", "desc", 1, Option<string>.None(), []),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.Unauthorized, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReplacesAndReturnsRecipe_WhenAuthorized()
	{
		var id = Guid.NewGuid();
		var now = DateTime.UtcNow;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Recipes.Add(new RecipeEntity
			{
				Id = id,
				FamilyGroupId = TestIds.Family("u1"),
				ContributedByUserId = "u1",
				Name = "Old",
				Description = "old",
				Servings = 2,
				Ingredients = [],
				CreatedAt = now.AddDays(-1),
				UpdatedAt = now.AddDays(-1)
			});
		});

		var handler = new UpdateRecipeCommandHandler(context);
		var result = await handler.HandleAsync(
			new UpdateRecipeCommand(id.ToString(),
				TestIds.Family("u1"),
				"New Name",
				"New Desc",
				6,
				Option<string>.Some("https://example.com/new"),
				[new Ingredient("Pepper", 1, "tbsp"), new Ingredient("Salt", 1, "tsp", true)]),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal(id.ToString(), result.Value!.Id);
		Assert.Equal("New Name", context.Recipes.Single().Name);
		Assert.Equal(6, context.Recipes.Single().Servings);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseFailure_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new UpdateRecipeCommandHandler(context);
		var result = await handler.HandleAsync(
			new UpdateRecipeCommand(Guid.NewGuid().ToString(), TestIds.Family("u1"), "Name", "desc", 1, Option<string>.None(), []),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
