using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Recipes.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.Recipes.Queries;

public class GetRecipeByIdTests
{
	[Fact]
	public async Task HandleAsync_ReturnsSuccess_WhenRecipeExistsForUser()
	{
		var id = Guid.NewGuid();
		var now = DateTime.UtcNow;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Recipes.Add(new RecipeEntity
			{
				Id = id,
				UserId = "u1",
				Name = "Soup",
				Description = "Warm",
				Servings = 2,
				Ingredients =
				[
					new IngredientData { Name = "Water", Quantity = 2, Unit = "cups" },
					new IngredientData { Name = "Salt", Quantity = 1, Unit = "pinch", IsPantryStaple = true }
				],
				CreatedAt = now,
				UpdatedAt = now
			});
		});

		var handler = new GetRecipeByIdQueryHandler(context);
		var result = await handler.HandleAsync(new GetRecipeByIdQuery(id.ToString(), "u1"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal(id.ToString(), result.Value!.Id);
		Assert.Equal(2, result.Value.Ingredients.Count);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenRecipeMissing()
	{
		var handler = new GetRecipeByIdQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetRecipeByIdQuery(Guid.NewGuid().ToString(), "u1"), TestContext.Current.CancellationToken);

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
				UserId = "u2",
				Name = "Soup",
				Description = "Warm",
				Servings = 1,
				Ingredients = [],
				CreatedAt = now,
				UpdatedAt = now
			});
		});

		var handler = new GetRecipeByIdQueryHandler(context);
		var result = await handler.HandleAsync(new GetRecipeByIdQuery(id.ToString(), "u1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.Unauthorized, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new GetRecipeByIdQueryHandler(context);
		var result = await handler.HandleAsync(new GetRecipeByIdQuery(Guid.NewGuid().ToString(), "u1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
