using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Features.Recipes.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.Recipes.Queries;

public class GetAllRecipesTests
{
	[Fact]
	public async Task HandleAsync_ReturnsRecipes_WhenDocumentsExist()
	{
		var docs = new List<RecipeDocument>
		{
			new()
			{
				Id = "r1",
				UserId = "u1",
				Name = "Pasta",
				Description = "Simple",
				SourceUrl = "https://example.com",
				Ingredients = [new IngredientDocument { Name = "Noodles", Quantity = 1, Unit = "pack" }],
				CreatedAt = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
				UpdatedAt = new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc)
			}
		};

		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<RecipeDocument>)docs);
		var collection = new Mock<IMongoCollection<RecipeDocument>>();
		collection.Setup(c => c.FindAsync(
			It.IsAny<FilterDefinition<RecipeDocument>>(),
			It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(
			It.IsAny<FilterDefinition<RecipeDocument>>(),
			It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(),
			It.IsAny<CancellationToken>()))
			.Returns(cursor.Object);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<RecipeDocument>("recipes", null))
			.Returns(collection.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new GetAllRecipesQueryHandler(client.Object);
		var result = await handler.HandleAsync(new GetAllRecipesQuery("u1"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Single(result.Value);
		var recipe = result.Value[0];
		Assert.Equal("r1", recipe.Id);
		Assert.Equal("Pasta", recipe.Name);
		Assert.True(recipe.SourceUrl.HasValue);
		Assert.Equal("https://example.com", recipe.SourceUrl.Value);
		Assert.Single(recipe.Ingredients);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null))
			.Throws(new InvalidOperationException("db failed"));

		var handler = new GetAllRecipesQueryHandler(client.Object);
		var result = await handler.HandleAsync(new GetAllRecipesQuery("u1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error.Code);
	}
}
