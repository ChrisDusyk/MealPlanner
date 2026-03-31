using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.GroceryLists.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Queries;

public class GetGroceryListTests
{
	[Fact]
	public async Task HandleAsync_ReturnsList_WhenFound()
	{
		var doc = new GroceryListDocument
		{
			Id = "g1",
			UserId = "u1",
			WeekStart = "2026-02-23",
			Items = [new GroceryListItemDocument { Name = "Rice", Quantity = 1, Unit = "kg", IsChecked = false, SourceRecipeNames = [] }],
			CreatedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc)
		};

		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<GroceryListDocument>)new List<GroceryListDocument> { doc });
		var collection = new Mock<IMongoCollection<GroceryListDocument>>();
		collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).Returns(cursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GetGroceryListQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetGroceryListQuery("u1", new DateOnly(2026, 2, 25)), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal("g1", result.Value.Id);
		Assert.Equal(new DateOnly(2026, 2, 23), result.Value.WeekStart);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenMissing()
	{
		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<GroceryListDocument>)Array.Empty<GroceryListDocument>());
		var collection = new Mock<IMongoCollection<GroceryListDocument>>();
		collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).Returns(cursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GetGroceryListQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetGroceryListQuery("u1", new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.NotFound, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));

		var handler = new GetGroceryListQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetGroceryListQuery("u1", new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
