using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Commands;

public class ToggleGroceryListItemTests
{
	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenListMissing()
	{
		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<GroceryListDocument>)Array.Empty<GroceryListDocument>());
		var collection = new Mock<IMongoCollection<GroceryListDocument>>();
		collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).Returns(cursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new ToggleGroceryListItemCommandHandler(client.Object);
		var result = await handler.HandleAsync(new ToggleGroceryListItemCommand("u1", new DateOnly(2026, 2, 23), 0), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenIndexOutOfRange()
	{
		var doc = new GroceryListDocument { Id = "g1", UserId = "u1", WeekStart = "2026-02-23", Items = [], CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<GroceryListDocument>)new List<GroceryListDocument> { doc });
		var collection = new Mock<IMongoCollection<GroceryListDocument>>();
		collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).Returns(cursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new ToggleGroceryListItemCommandHandler(client.Object);
		var result = await handler.HandleAsync(new ToggleGroceryListItemCommand("u1", new DateOnly(2026, 2, 23), 5), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_TogglesItem_WhenIndexValid()
	{
		var doc = new GroceryListDocument
		{
			Id = "g1",
			UserId = "u1",
			WeekStart = "2026-02-23",
			Items = [new GroceryListItemDocument { Name = "Rice", Quantity = 1, Unit = "kg", IsChecked = false, SourceRecipeNames = [] }],
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		GroceryListDocument? replaced = null;

		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<GroceryListDocument>)new List<GroceryListDocument> { doc });
		var collection = new Mock<IMongoCollection<GroceryListDocument>>();
		collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).Returns(cursor.Object);
		collection.Setup(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<GroceryListDocument>(), It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
			.Callback<FilterDefinition<GroceryListDocument>, GroceryListDocument, ReplaceOptions, CancellationToken>((_, d, _, _) => replaced = d)
			.ReturnsAsync(Mock.Of<ReplaceOneResult>());

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new ToggleGroceryListItemCommandHandler(client.Object);
		var result = await handler.HandleAsync(new ToggleGroceryListItemCommand("u1", new DateOnly(2026, 2, 23), 0), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(replaced);
		Assert.True(replaced.Items[0].IsChecked);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));

		var handler = new ToggleGroceryListItemCommandHandler(client.Object);
		var result = await handler.HandleAsync(new ToggleGroceryListItemCommand("u1", new DateOnly(2026, 2, 23), 0), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
