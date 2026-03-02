using MealPlanner.Api.Features.Recipes.Commands;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.Recipes.Commands;

public class UpdateRecipeTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenNameIsMissing()
	{
		var handler = new UpdateRecipeCommandHandler(new Mock<IMongoClient>().Object);
		var command = new UpdateRecipeCommand("r1", "u1", " ", "desc", 1, Option<string>.None(), []);

		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenServingsIsLessThanOne()
	{
		var handler = new UpdateRecipeCommandHandler(new Mock<IMongoClient>().Object);
		var command = new UpdateRecipeCommand("r1", "u1", "Updated", "desc", 0, Option<string>.None(), []);

		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
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

		var handler = new UpdateRecipeCommandHandler(client.Object);
		var command = new UpdateRecipeCommand("missing", "u1", "Updated", "desc", 1, Option<string>.None(), []);
		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.NotFound, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsUnauthorized_WhenRecipeBelongsToAnotherUser()
	{
		var existing = new RecipeDocument
		{
			Id = "r1",
			UserId = "u2",
			Name = "Old",
			Description = "old",
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
			Ingredients = []
		};

		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<RecipeDocument>)new List<RecipeDocument> { existing });
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

		var handler = new UpdateRecipeCommandHandler(client.Object);
		var command = new UpdateRecipeCommand("r1", "u1", "Updated", "desc", 1, Option<string>.None(), []);
		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.Unauthorized, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReplacesAndReturnsRecipe_WhenAuthorized()
	{
		var existing = new RecipeDocument
		{
			Id = "r1",
			UserId = "u1",
			Name = "Old",
			Description = "old",
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
			Ingredients = []
		};

		RecipeDocument? replaced = null;
		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<RecipeDocument>)new List<RecipeDocument> { existing });
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
		collection.Setup(c => c.ReplaceOneAsync(
			It.IsAny<FilterDefinition<RecipeDocument>>(),
			It.IsAny<RecipeDocument>(),
			It.IsAny<ReplaceOptions>(),
			It.IsAny<CancellationToken>()))
			.Callback<FilterDefinition<RecipeDocument>, RecipeDocument, ReplaceOptions, CancellationToken>((_, doc, _, _) => replaced = doc)
			.ReturnsAsync(Mock.Of<ReplaceOneResult>());

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<RecipeDocument>("recipes", null)).Returns(collection.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new UpdateRecipeCommandHandler(client.Object);
		var command = new UpdateRecipeCommand(
			"r1",
			"u1",
			"New Name",
			"New Desc",
			6,
			Option<string>.Some("https://example.com/new"),
			[new Ingredient("Pepper", 1, "tbsp"), new Ingredient("Salt", 1, "tsp", true)]);

		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.NotNull(replaced);
		Assert.Equal(existing.CreatedAt, replaced.CreatedAt);
		Assert.Equal("New Name", replaced.Name);
		Assert.Equal("New Desc", replaced.Description);
		Assert.Equal(6, replaced.Servings);
		Assert.Equal("https://example.com/new", replaced.SourceUrl);
		Assert.Equal("r1", result.Value.Id);
		Assert.True(result.Value.UpdatedAt >= existing.UpdatedAt);
		Assert.Contains(replaced.Ingredients, i => i.Name == "Pepper" && !i.IsPantryStaple);
		Assert.Contains(replaced.Ingredients, i => i.Name == "Salt" && i.IsPantryStaple);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseFailure_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null))
			.Throws(new Exception("boom"));

		var handler = new UpdateRecipeCommandHandler(client.Object);
		var command = new UpdateRecipeCommand("r1", "u1", "Name", "desc", 1, Option<string>.None(), []);
		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error.Code);
	}
}
