using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Commands;

public class PromotePantryStapleItemTests
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

		var handler = new PromotePantryStapleItemCommandHandler(client.Object);
		var result = await handler.HandleAsync(new PromotePantryStapleItemCommand("u1", new DateOnly(2026, 2, 23), 0), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenIndexOutOfRange()
	{
		var doc = new GroceryListDocument
		{
			Id = "g1",
			UserId = "u1",
			WeekStart = "2026-02-23",
			Items = [],
			PantryStapleItems = [new GroceryListItemDocument { Name = "Salt", Quantity = 1, Unit = "tsp", IsChecked = false, SourceRecipeNames = ["Pasta"] }],
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<GroceryListDocument>)new List<GroceryListDocument> { doc });
		var collection = new Mock<IMongoCollection<GroceryListDocument>>();
		collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).Returns(cursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new PromotePantryStapleItemCommandHandler(client.Object);
		var result = await handler.HandleAsync(new PromotePantryStapleItemCommand("u1", new DateOnly(2026, 2, 23), 5), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenIndexNegative()
	{
		var doc = new GroceryListDocument
		{
			Id = "g1",
			UserId = "u1",
			WeekStart = "2026-02-23",
			Items = [],
			PantryStapleItems = [new GroceryListItemDocument { Name = "Salt", Quantity = 1, Unit = "tsp", IsChecked = false, SourceRecipeNames = [] }],
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<GroceryListDocument>)new List<GroceryListDocument> { doc });
		var collection = new Mock<IMongoCollection<GroceryListDocument>>();
		collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).Returns(cursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new PromotePantryStapleItemCommandHandler(client.Object);
		var result = await handler.HandleAsync(new PromotePantryStapleItemCommand("u1", new DateOnly(2026, 2, 23), -1), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_MovesItemToMainList_WhenIndexValid()
	{
		var doc = new GroceryListDocument
		{
			Id = "g1",
			UserId = "u1",
			WeekStart = "2026-02-23",
			Items = [new GroceryListItemDocument { Name = "Tomato", Quantity = 2, Unit = "pcs", IsChecked = false, SourceRecipeNames = ["Pasta"] }],
			PantryStapleItems =
			[
				new GroceryListItemDocument { Name = "Salt", Quantity = 1, Unit = "tsp", IsChecked = false, SourceRecipeNames = ["Pasta"] },
				new GroceryListItemDocument { Name = "Olive Oil", Quantity = 2, Unit = "tbsp", IsChecked = false, SourceRecipeNames = ["Pasta"] }
			],
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

		var handler = new PromotePantryStapleItemCommandHandler(client.Object);
		var result = await handler.HandleAsync(new PromotePantryStapleItemCommand("u1", new DateOnly(2026, 2, 23), 0), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(replaced);
		// Salt should be moved from PantryStapleItems to Items
		Assert.Equal(2, replaced.Items.Count);
		Assert.Contains(replaced.Items, i => i.Name == "Tomato");
		Assert.Contains(replaced.Items, i => i.Name == "Salt" && !i.IsChecked);
		// Only Olive Oil should remain in PantryStapleItems
		Assert.Single(replaced.PantryStapleItems);
		Assert.Equal("Olive Oil", replaced.PantryStapleItems[0].Name);
		// Domain result should reflect the same state
		Assert.NotNull(result.Value);
		Assert.Equal(2, result.Value.Items.Count);
		Assert.Single(result.Value.PantryStapleItems);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));

		var handler = new PromotePantryStapleItemCommandHandler(client.Object);
		var result = await handler.HandleAsync(new PromotePantryStapleItemCommand("u1", new DateOnly(2026, 2, 23), 0), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
