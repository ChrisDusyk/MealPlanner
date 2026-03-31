using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Queries;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.MealPlans.Queries;

public class GetSharedWithMeTests
{
	private static MealPlanDocument PlanDoc(string owner) => new()
	{
		Id = "mp1",
		UserId = owner,
		WeekStart = "2026-02-23",
		Days = [new DayPlanDocument { Day = "Monday", Slots = new Dictionary<string, List<MealSlotItemDocument>> { ["Breakfast"] = [], ["Lunch"] = [], ["Supper"] = [], ["Snacks"] = [] } }],
		CreatedAt = DateTime.UtcNow,
		UpdatedAt = DateTime.UtcNow
	};

	[Fact]
	public async Task HandleAsync_ReturnsEmpty_WhenNoShares()
	{
		var shareCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanShareDocument>)Array.Empty<MealPlanShareDocument>());
		var shares = new Mock<IMongoCollection<MealPlanShareDocument>>();
		shares.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(), It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(shareCursor.Object);
		shares.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(), It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>())).Returns(shareCursor.Object);

		var plans = new Mock<IMongoCollection<MealPlanDocument>>();
		var users = new Mock<IMongoCollection<UserDocument>>();
		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(plans.Object);
		db.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GetSharedWithMeQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetSharedWithMeQuery("recipient1", "2026-02-23"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Empty(result.Value!);
	}

	[Fact]
	public async Task HandleAsync_ReturnsSharedMealPlans_WhenDataExists()
	{
		var shareDoc = new MealPlanShareDocument
		{
			Id = "s1",
			OwnerUserId = "owner1",
			SharedWithUserId = "recipient1",
			WeekStart = "2026-02-23",
			Permission = nameof(SharePermission.ReadWrite),
			SharedAt = DateTime.UtcNow,
			DismissedByRecipient = false
		};
		var ownerDoc = new UserDocument { Auth0UserId = "owner1", Name = "Morgan", Email = "morgan@example.com" };

		var shareCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanShareDocument>)new List<MealPlanShareDocument> { shareDoc });
		var ownerCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<UserDocument>)new List<UserDocument> { ownerDoc });
		var planCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanDocument>)new List<MealPlanDocument> { PlanDoc("owner1") });

		var shares = new Mock<IMongoCollection<MealPlanShareDocument>>();
		shares.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(), It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(shareCursor.Object);
		shares.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(), It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>())).Returns(shareCursor.Object);

		var users = new Mock<IMongoCollection<UserDocument>>();
		users.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<UserDocument>>(), It.IsAny<FindOptions<UserDocument, UserDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(ownerCursor.Object);
		users.Setup(c => c.FindSync(It.IsAny<FilterDefinition<UserDocument>>(), It.IsAny<FindOptions<UserDocument, UserDocument>>(), It.IsAny<CancellationToken>())).Returns(ownerCursor.Object);

		var plans = new Mock<IMongoCollection<MealPlanDocument>>();
		plans.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(planCursor.Object);
		plans.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).Returns(planCursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(plans.Object);
		db.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GetSharedWithMeQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetSharedWithMeQuery("recipient1", "2026-02-23"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!);
		Assert.Equal("Morgan", result.Value![0].OwnerName);
		Assert.Equal("morgan@example.com", result.Value![0].OwnerEmail);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));

		var handler = new GetSharedWithMeQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetSharedWithMeQuery("recipient1", "2026-02-23"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
