using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Recipes.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.Recipes.Queries;

public class GetAllRecipesTests
{
	[Fact]
	public async Task HandleAsync_ReturnsRecipes_WhenDocumentsExist()
	{
		var now = DateTime.UtcNow;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Recipes.Add(new RecipeEntity
			{
				Id = Guid.NewGuid(),
				UserId = "u1",
				Name = "Pasta",
				Description = "Simple",
				Servings = 2,
				SourceUrl = "https://example.com",
				Ingredients =
				[
					new IngredientData { Name = "Noodles", Quantity = 1, Unit = "pack" },
					new IngredientData { Name = "Soy Sauce", Quantity = 2, Unit = "tbsp", IsPantryStaple = true }
				],
				CreatedAt = now,
				UpdatedAt = now
			});
		});

		var handler = new GetAllRecipesQueryHandler(context);
		var result = await handler.HandleAsync(new GetAllRecipesQuery("u1"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!);
		Assert.Equal("Pasta", result.Value![0].Name);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new GetAllRecipesQueryHandler(context);
		var result = await handler.HandleAsync(new GetAllRecipesQuery("u1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
