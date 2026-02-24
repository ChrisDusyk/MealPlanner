using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Queries;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.MealPlans.Queries;

public class GetSharesForMealPlanTests
{
	[Fact]
	public async Task HandleAsync_ReturnsEmpty_WhenNoShares()
	{
		var shareCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanShareDocument>)Array.Empty<MealPlanShareDocument>());
		var shares = new Mock<IMongoCollection<MealPlanShareDocument>>();
		shares.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(), It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(shareCursor.Object);
		shares.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(), It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>())).Returns(shareCursor.Object);

		var users = new Mock<IMongoCollection<UserDocument>>();
		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		db.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GetSharesForMealPlanQueryHandler(client.Object);
		var result = await handler.HandleAsync(new GetSharesForMealPlanQuery("owner1", "2026-02-23"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Empty(result.Value!);
	}

	[Fact]
	public async Task HandleAsync_ReturnsEnrichedShares_WhenFound()
	{
		var shareDoc = new MealPlanShareDocument
		{
			Id = "s1",
			OwnerUserId = "owner1",
			SharedWithUserId = "recipient1",
			WeekStart = "2026-02-23",
			Permission = nameof(SharePermission.ReadOnly),
			SharedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
			DismissedByRecipient = false
		};
		var userDoc = new UserDocument { Id = "u1", Auth0UserId = "recipient1", Name = "Alex", Email = "alex@example.com" };

		var shareCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanShareDocument>)new List<MealPlanShareDocument> { shareDoc });
		var userCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<UserDocument>)new List<UserDocument> { userDoc });

		var shares = new Mock<IMongoCollection<MealPlanShareDocument>>();
		shares.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(), It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(shareCursor.Object);
		shares.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(), It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>())).Returns(shareCursor.Object);

		var users = new Mock<IMongoCollection<UserDocument>>();
		users.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<UserDocument>>(), It.IsAny<FindOptions<UserDocument, UserDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(userCursor.Object);
		users.Setup(c => c.FindSync(It.IsAny<FilterDefinition<UserDocument>>(), It.IsAny<FindOptions<UserDocument, UserDocument>>(), It.IsAny<CancellationToken>())).Returns(userCursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		db.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GetSharesForMealPlanQueryHandler(client.Object);
		var result = await handler.HandleAsync(new GetSharesForMealPlanQuery("owner1", "2026-02-23"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!);
		Assert.Equal("Alex", result.Value![0].RecipientName);
		Assert.Equal("alex@example.com", result.Value![0].RecipientEmail);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));

		var handler = new GetSharesForMealPlanQueryHandler(client.Object);
		var result = await handler.HandleAsync(new GetSharesForMealPlanQuery("owner1", "2026-02-23"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
