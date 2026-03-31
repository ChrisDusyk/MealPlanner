using MealPlanner.Api.Features.MealPlans.Commands;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.MealPlans.Commands;

public class RemoveSlotItemTests
{
	private static MealPlanDocument BaseDoc() => new()
	{
		Id = "mp1",
		UserId = "u1",
		WeekStart = "2026-02-23",
		Days =
		[
			new DayPlanDocument
			{
				Day = "Monday",
				Slots = new Dictionary<string, List<MealSlotItemDocument>>
				{
					["Breakfast"] = [new MealSlotItemDocument { RecipeId = "r1", Name = "Oats" }],
					["Lunch"] = [],
					["Supper"] = [],
					["Snacks"] = []
				}
			}
		],
		CreatedAt = DateTime.UtcNow,
		UpdatedAt = DateTime.UtcNow
	};

	private static Mock<IMongoCollection<MealPlanDocument>> CollectionWithDoc(MealPlanDocument doc)
	{
		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanDocument>)new List<MealPlanDocument> { doc });
		var collection = new Mock<IMongoCollection<MealPlanDocument>>();
		collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).Returns(cursor.Object);
		collection.Setup(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<MealPlanDocument>(), It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<ReplaceOneResult>());
		return collection;
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenPlanMissing()
	{
		var emptyCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanDocument>)Array.Empty<MealPlanDocument>());
		var collection = new Mock<IMongoCollection<MealPlanDocument>>();
		collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(emptyCursor.Object);
		collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).Returns(emptyCursor.Object);
		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new RemoveSlotItemCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new RemoveSlotItemCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, 0), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenIndexOutOfRange()
	{
		var collection = CollectionWithDoc(BaseDoc());
		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new RemoveSlotItemCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new RemoveSlotItemCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, 5), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_RemovesItem_WhenIndexValid()
	{
		MealPlanDocument? replaced = null;
		var doc = BaseDoc();
		var collection = CollectionWithDoc(doc);
		collection.Setup(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<MealPlanDocument>(), It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
			.Callback<FilterDefinition<MealPlanDocument>, MealPlanDocument, ReplaceOptions, CancellationToken>((_, d, _, _) => replaced = d)
			.ReturnsAsync(Mock.Of<ReplaceOneResult>());
		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new RemoveSlotItemCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new RemoveSlotItemCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, 0), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(replaced);
		Assert.Empty(replaced.Days[0].Slots["Breakfast"]);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));
		var handler = new RemoveSlotItemCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(new RemoveSlotItemCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, 0), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
