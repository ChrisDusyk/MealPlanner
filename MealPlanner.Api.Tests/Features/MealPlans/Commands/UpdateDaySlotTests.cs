using MealPlanner.Api.Features.MealPlans.Commands;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.MealPlans.Commands;

public class UpdateDaySlotTests
{
	private static MealPlanDocument ExistingDoc() => new()
	{
		Id = "mp1",
		UserId = "u1",
		WeekStart = "2026-02-23",
		Days = [new DayPlanDocument { Day = "Monday", Slots = new Dictionary<string, List<MealSlotItemDocument>> { ["Breakfast"] = [], ["Lunch"] = [], ["Supper"] = [], ["Snacks"] = [] } }],
		CreatedAt = DateTime.UtcNow,
		UpdatedAt = DateTime.UtcNow
	};

	[Fact]
	public async Task HandleAsync_UpdatesSlot_WhenPlanAndDayExist()
	{
		var doc = ExistingDoc();
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

		var handler = new UpdateDaySlotCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new UpdateDaySlotCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, [new MealSlotItem(Option<string>.Some("r1"), "Oats", 4)]),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(replaced);
		Assert.Single(replaced.Days[0].Slots["Breakfast"]);
		Assert.Equal("r1", replaced.Days[0].Slots["Breakfast"][0].RecipeId);
		Assert.Equal(4, replaced.Days[0].Slots["Breakfast"][0].Servings);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenDayMissing()
	{
		var doc = ExistingDoc();
		doc.Days.Clear();
		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanDocument>)new List<MealPlanDocument> { doc });
		var collection = new Mock<IMongoCollection<MealPlanDocument>>();
		collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).Returns(cursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new UpdateDaySlotCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new UpdateDaySlotCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, []),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenAnyItemServingsIsLessThanOne()
	{
		var handler = new UpdateDaySlotCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new UpdateDaySlotCommand(
				"u1",
				new DateOnly(2026, 2, 23),
				DayOfWeek.Monday,
				MealCategory.Breakfast,
				[new MealSlotItem(Option<string>.Some("r1"), "Oats", 0)]),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));

		var handler = new UpdateDaySlotCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new UpdateDaySlotCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, []), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
