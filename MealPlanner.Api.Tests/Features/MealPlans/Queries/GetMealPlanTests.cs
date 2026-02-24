using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.MealPlans.Queries;

public class GetMealPlanTests
{
	private static MealPlanDocument ExistingDocument() => new()
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
		CreatedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
		UpdatedAt = new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc)
	};

	[Fact]
	public async Task HandleAsync_ReturnsExistingPlan_WhenFound()
	{
		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanDocument>)new List<MealPlanDocument> { ExistingDocument() });
		var collection = new Mock<IMongoCollection<MealPlanDocument>>();
		collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).Returns(cursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GetMealPlanQueryHandler(client.Object);
		var result = await handler.HandleAsync(new GetMealPlanQuery("u1", new DateOnly(2026, 2, 25)), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal("mp1", result.Value.Id);
		Assert.Equal(new DateOnly(2026, 2, 23), result.Value.WeekStart);
	}

	[Fact]
	public async Task HandleAsync_CreatesEmptyPlan_WhenMissing()
	{
		MealPlanDocument? inserted = null;
		var emptyCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanDocument>)Array.Empty<MealPlanDocument>());
		var collection = new Mock<IMongoCollection<MealPlanDocument>>();
		collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(emptyCursor.Object);
		collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).Returns(emptyCursor.Object);
		collection.Setup(c => c.InsertOneAsync(It.IsAny<MealPlanDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
			.Callback<MealPlanDocument, InsertOneOptions, CancellationToken>((d, _, _) => inserted = d)
			.Returns(Task.CompletedTask);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GetMealPlanQueryHandler(client.Object);
		var result = await handler.HandleAsync(new GetMealPlanQuery("u1", new DateOnly(2026, 2, 26)), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.NotNull(inserted);
		Assert.Equal("2026-02-23", inserted.WeekStart);
		Assert.Equal(7, inserted.Days.Count);
		Assert.All(inserted.Days, d => Assert.Equal(4, d.Slots.Count));
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));

		var handler = new GetMealPlanQueryHandler(client.Object);
		var result = await handler.HandleAsync(new GetMealPlanQuery("u1", new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
