using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Features.Recipes.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.Recipes.Queries;

public class GetRecipeByIdTests
{
	[Fact]
	public async Task HandleAsync_ReturnsSuccess_WhenRecipeExistsForUser()
	{
		var doc = new RecipeDocument
		{
			Id = "r1",
			UserId = "u1",
			Name = "Soup",
			Description = "Warm",
			SourceUrl = null,
			Ingredients =
			[
				new IngredientDocument { Name = "Water", Quantity = 2, Unit = "cups" },
				new IngredientDocument { Name = "Salt", Quantity = 1, Unit = "pinch", IsPantryStaple = true }
			],
			CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc)
		};

		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<RecipeDocument>)new List<RecipeDocument> { doc });
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
		database.Setup(d => d.GetCollection<RecipeDocument>("recipes", null)).Returns(collection.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new GetRecipeByIdQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetRecipeByIdQuery("r1", "u1"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal("r1", result.Value.Id);
		Assert.False(result.Value.SourceUrl.HasValue);
		Assert.Equal(2, result.Value.Ingredients.Count);
		Assert.False(result.Value.Ingredients[0].IsPantryStaple);
		Assert.True(result.Value.Ingredients[1].IsPantryStaple);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenRecipeMissing()
	{
		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<RecipeDocument>)Array.Empty<RecipeDocument>());
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
		database.Setup(d => d.GetCollection<RecipeDocument>("recipes", null)).Returns(collection.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new GetRecipeByIdQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetRecipeByIdQuery("missing", "u1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.NotFound, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsUnauthorized_WhenRecipeBelongsToAnotherUser()
	{
		var doc = new RecipeDocument
		{
			Id = "r1",
			UserId = "u2",
			Name = "Soup",
			Description = "Warm",
			Ingredients = []
		};

		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<RecipeDocument>)new List<RecipeDocument> { doc });
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
		database.Setup(d => d.GetCollection<RecipeDocument>("recipes", null)).Returns(collection.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new GetRecipeByIdQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetRecipeByIdQuery("r1", "u1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.Unauthorized, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null))
			.Throws(new Exception("boom"));

		var handler = new GetRecipeByIdQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetRecipeByIdQuery("r1", "u1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error.Code);
	}
}
