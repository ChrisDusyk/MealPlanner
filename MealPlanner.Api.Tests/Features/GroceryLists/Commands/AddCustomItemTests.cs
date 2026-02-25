using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Commands;

public class AddCustomItemTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenItemNameEmpty()
	{
		var handler = new AddCustomItemCommandHandler(new Mock<IMongoClient>().Object);
		var result = await handler.HandleAsync(new AddCustomItemCommand("u1", new DateOnly(2026, 2, 23), "  "),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenListMissing()
	{
		var cursor =
			MongoTestHelpers.CreateCursor(Array.Empty<GroceryListDocument>());
		var collection = new Mock<IMongoCollection<GroceryListDocument>>();
		collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(cursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new AddCustomItemCommandHandler(client.Object);
		var result = await handler.HandleAsync(new AddCustomItemCommand("u1", new DateOnly(2026, 2, 23), "Milk"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_AddsTrimmedCustomItem_WhenListExists()
	{
		var doc = new GroceryListDocument
		{
			Id = "g1",
			UserId = "u1",
			WeekStart = "2026-02-23",
			Items = [],
			CreatedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc)
		};
		GroceryListDocument? replaced = null;

		var cursor =
			MongoTestHelpers.CreateCursor(new List<GroceryListDocument> { doc });
		var collection = new Mock<IMongoCollection<GroceryListDocument>>();
		collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(cursor.Object);
		collection.Setup(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<GroceryListDocument>(), It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
			.Callback<FilterDefinition<GroceryListDocument>, GroceryListDocument, ReplaceOptions, CancellationToken>((_,
				d, _, _) => replaced = d)
			.ReturnsAsync(Mock.Of<ReplaceOneResult>());

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new AddCustomItemCommandHandler(client.Object);
		var result = await handler.HandleAsync(new AddCustomItemCommand("u1", new DateOnly(2026, 2, 23), "  Milk  "),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(replaced);
		Assert.Contains(replaced.Items, i => i.Name == "Milk" && i.Quantity == 0 && i.Unit == string.Empty);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));

		var handler = new AddCustomItemCommandHandler(client.Object);
		var result = await handler.HandleAsync(new AddCustomItemCommand("u1", new DateOnly(2026, 2, 23), "Milk"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_AddsCustomItem_WhenSharedWithReadWritePermission()
	{
		var doc = new GroceryListDocument
		{
			Id = "g1",
			UserId = "owner1",
			WeekStart = "2026-02-23",
			Items = [],
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		var share = new GroceryListShareDocument
		{
			Id = "s1",
			OwnerUserId = "owner1",
			SharedWithUserId = "guest1",
			WeekStart = "2026-02-23",
			Permission = SharePermission.ReadWrite.ToString(),
			DismissedByRecipient = false,
			SharedAt = DateTime.UtcNow
		};

		GroceryListDocument? replaced = null;

		var listCursor =
			MongoTestHelpers.CreateCursor(new List<GroceryListDocument> { doc });
		var shareCursor =
			MongoTestHelpers.CreateCursor(new List<GroceryListShareDocument> { share });

		var groceryCollection = new Mock<IMongoCollection<GroceryListDocument>>();
		groceryCollection
			.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(listCursor.Object);
		groceryCollection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(listCursor.Object);
		groceryCollection.Setup(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<GroceryListDocument>(), It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
			.Callback<FilterDefinition<GroceryListDocument>, GroceryListDocument, ReplaceOptions, CancellationToken>((_,
				d, _, _) => replaced = d)
			.ReturnsAsync(Mock.Of<ReplaceOneResult>());

		var sharesCollection = new Mock<IMongoCollection<GroceryListShareDocument>>();
		sharesCollection
			.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>())).ReturnsAsync(shareCursor.Object);
		sharesCollection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>()))
			.Returns(shareCursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(groceryCollection.Object);
		db.Setup(d => d.GetCollection<GroceryListShareDocument>("grocerylist_shares", null))
			.Returns(sharesCollection.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new AddCustomItemCommandHandler(client.Object);
		var result = await handler.HandleAsync(
			new AddCustomItemCommand("guest1", new DateOnly(2026, 2, 23), "Bread", "owner1"),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(replaced);
		Assert.Contains(replaced.Items, i => i.Name == "Bread");
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenSharedPermissionIsReadOnly()
	{
		var share = new GroceryListShareDocument
		{
			Id = "s1",
			OwnerUserId = "owner1",
			SharedWithUserId = "guest1",
			WeekStart = "2026-02-23",
			Permission = SharePermission.ReadOnly.ToString(),
			DismissedByRecipient = false,
			SharedAt = DateTime.UtcNow
		};

		var shareCursor =
			MongoTestHelpers.CreateCursor(new List<GroceryListShareDocument> { share });

		var sharesCollection = new Mock<IMongoCollection<GroceryListShareDocument>>();
		sharesCollection
			.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>())).ReturnsAsync(shareCursor.Object);
		sharesCollection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>()))
			.Returns(shareCursor.Object);

		var groceryCollection = new Mock<IMongoCollection<GroceryListDocument>>();

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(groceryCollection.Object);
		db.Setup(d => d.GetCollection<GroceryListShareDocument>("grocerylist_shares", null))
			.Returns(sharesCollection.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new AddCustomItemCommandHandler(client.Object);
		var result = await handler.HandleAsync(
			new AddCustomItemCommand("guest1", new DateOnly(2026, 2, 23), "Bread", "owner1"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}
}
