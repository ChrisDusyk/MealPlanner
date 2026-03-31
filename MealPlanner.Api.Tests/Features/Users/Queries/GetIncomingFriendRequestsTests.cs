using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Features.Users.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.Users.Queries;

public class GetIncomingFriendRequestsTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenRecipientMissing()
	{
		var handler = new GetIncomingFriendRequestsQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetIncomingFriendRequestsQuery(" "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsIncomingRequestSummaries()
	{
		var request = new FriendRequestDocument
		{
			Id = "r1",
			RequesterUserId = "auth0|you",
			RecipientUserId = "auth0|me",
			CreatedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
		};
		var requestCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<FriendRequestDocument>)new[] { request });
		var requests = new Mock<IMongoCollection<FriendRequestDocument>>();
		requests.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<FriendRequestDocument>>(), It.IsAny<FindOptions<FriendRequestDocument, FriendRequestDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(requestCursor.Object);
		requests.Setup(c => c.FindSync(It.IsAny<FilterDefinition<FriendRequestDocument>>(), It.IsAny<FindOptions<FriendRequestDocument, FriendRequestDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(requestCursor.Object);

		var requester = new UserDocument { Auth0UserId = "auth0|you", Name = "You", Email = "you@example.com" };
		var userCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<UserDocument>)new[] { requester });
		var users = new Mock<IMongoCollection<UserDocument>>();
		users.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<UserDocument>>(), It.IsAny<FindOptions<UserDocument, UserDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(userCursor.Object);
		users.Setup(c => c.FindSync(It.IsAny<FilterDefinition<UserDocument>>(), It.IsAny<FindOptions<UserDocument, UserDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(userCursor.Object);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<FriendRequestDocument>("friend_requests", null)).Returns(requests.Object);
		database.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new GetIncomingFriendRequestsQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetIncomingFriendRequestsQuery("auth0|me"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!);
		Assert.Equal("r1", result.Value![0].RequestId);
		Assert.Equal("auth0|you", result.Value[0].UserId);
	}
}
