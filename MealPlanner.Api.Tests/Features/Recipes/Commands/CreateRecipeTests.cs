using MealPlanner.Api.Features.Recipes.Commands;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.Recipes.Commands;

public class CreateRecipeTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenNameIsMissing()
	{
		var handler = new CreateRecipeCommandHandler(new Mock<IMongoClient>().Object);
		var command = new CreateRecipeCommand("u1", " ", "desc", 1, Option<string>.None(), []);

		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_CreatesRecipe_WhenCommandValid()
	{
		RecipeDocument? inserted = null;

		var collection = new Mock<IMongoCollection<RecipeDocument>>();
		collection.Setup(c => c.InsertOneAsync(
			It.IsAny<RecipeDocument>(),
			It.IsAny<InsertOneOptions>(),
			It.IsAny<CancellationToken>()))
			.Callback<RecipeDocument, InsertOneOptions?, CancellationToken>((doc, _, _) => inserted = doc)
			.Returns(Task.CompletedTask);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<RecipeDocument>("recipes", null)).Returns(collection.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new CreateRecipeCommandHandler(client.Object);
		var command = new CreateRecipeCommand(
			"u1",
			"Chili",
			"Spicy",
			4,
			Option<string>.Some("https://example.com/chili"),
			[new Ingredient("Beans", 2, "cups")]);

		var before = DateTime.UtcNow;
		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);
		var after = DateTime.UtcNow;

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.NotNull(inserted);
		Assert.Equal("u1", inserted.UserId);
		Assert.Equal("Chili", inserted.Name);
		Assert.Equal(4, inserted.Servings);
		Assert.InRange(inserted.CreatedAt, before.AddSeconds(-1), after.AddSeconds(1));
		Assert.InRange(inserted.UpdatedAt, before.AddSeconds(-1), after.AddSeconds(1));
		Assert.True(result.Value.SourceUrl.HasValue);
		Assert.Equal("https://example.com/chili", result.Value.SourceUrl.Value);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseFailure_WhenInsertThrows()
	{
		var collection = new Mock<IMongoCollection<RecipeDocument>>();
		collection.Setup(c => c.InsertOneAsync(
			It.IsAny<RecipeDocument>(),
			It.IsAny<InsertOneOptions>(),
			It.IsAny<CancellationToken>()))
			.ThrowsAsync(new Exception("insert failed"));

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<RecipeDocument>("recipes", null)).Returns(collection.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new CreateRecipeCommandHandler(client.Object);
		var command = new CreateRecipeCommand("u1", "Chili", "Spicy", 1, Option<string>.None(), []);
		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error.Code);
	}
}
