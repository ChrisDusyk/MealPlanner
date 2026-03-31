using MealPlanner.Api.Features.MealPlans.Commands;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.MealPlans.Commands;

public class CopyCategoryTests
{
	private static MealPlanDocument DocWithSourceItems() => new()
	{
		Id = "mp1",
		UserId = "u1",
		WeekStart = "2026-02-23",
		Days =
		[
			new DayPlanDocument { Day = "Monday", Slots = new Dictionary<string, List<MealSlotItemDocument>> { ["Breakfast"] = [new MealSlotItemDocument { RecipeId = "r1", Name = "Oats", Servings = 2 }], ["Lunch"] = [], ["Supper"] = [], ["Snacks"] = [] } },
			new DayPlanDocument { Day = "Tuesday", Slots = new Dictionary<string, List<MealSlotItemDocument>> { ["Breakfast"] = [], ["Lunch"] = [], ["Supper"] = [], ["Snacks"] = [] } }
		],
		CreatedAt = DateTime.UtcNow,
		UpdatedAt = DateTime.UtcNow
	};

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenNoTargets()
	{
		var handler = new CopyCategoryCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new CopyCategoryCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, []), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
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

		var handler = new CopyCategoryCommandHandler(client.Object);
		var result = await handler.HandleAsync(new CopyCategoryCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, [DayOfWeek.Tuesday]), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_CopiesSourceCategoryToTargets()
	{
		var doc = DocWithSourceItems();
		MealPlanDocument? replaced = null;
		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanDocument>)new List<MealPlanDocument> { doc });
		var collection = new Mock<IMongoCollection<MealPlanDocument>>();
		collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).Returns(cursor.Object);
		collection.Setup(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<MealPlanDocument>(), It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
			.Callback<FilterDefinition<MealPlanDocument>, MealPlanDocument, ReplaceOptions, CancellationToken>((_, d, _, _) => replaced = d)
			.ReturnsAsync(Mock.Of<ReplaceOneResult>());
		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new CopyCategoryCommandHandler(client.Object);
		var result = await handler.HandleAsync(new CopyCategoryCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, [DayOfWeek.Tuesday]), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(replaced);
		var copiedItems = replaced.Days.First(d => d.Day == "Tuesday").Slots["Breakfast"];
		Assert.Single(copiedItems);
		Assert.Equal(2, copiedItems[0].Servings);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));

		var handler = new CopyCategoryCommandHandler(client.Object);
		var result = await handler.HandleAsync(new CopyCategoryCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, [DayOfWeek.Tuesday]), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
