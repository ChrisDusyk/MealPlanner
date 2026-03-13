using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.Users.Commands;

public class RejectFriendRequestTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenRequestIdMissing()
	{
		var handler = new RejectFriendRequestCommandHandler(new Mock<IMongoClient>().Object);
		var result = await handler.HandleAsync(new RejectFriendRequestCommand("auth0|me", " "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenRequestNotFound()
	{
		var requests = new Mock<IMongoCollection<FriendRequestDocument>>();
		var emptyCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<FriendRequestDocument>)Array.Empty<FriendRequestDocument>());
		requests.Setup(c => c.FindAsync(
			It.IsAny<FilterDefinition<FriendRequestDocument>>(),
			It.IsAny<FindOptions<FriendRequestDocument, FriendRequestDocument>>(),
			It.IsAny<CancellationToken>())).ReturnsAsync(emptyCursor.Object);
		requests.Setup(c => c.FindSync(
			It.IsAny<FilterDefinition<FriendRequestDocument>>(),
			It.IsAny<FindOptions<FriendRequestDocument, FriendRequestDocument>>(),
			It.IsAny<CancellationToken>())).Returns(emptyCursor.Object);
		requests.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<FriendRequestDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new DeleteResult.Acknowledged(0));

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<FriendRequestDocument>("friend_requests", null)).Returns(requests.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new RejectFriendRequestCommandHandler(client.Object);
		var result = await handler.HandleAsync(new RejectFriendRequestCommand("auth0|me", "r1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsSuccess_WhenDeleted()
	{
		var request = new FriendRequestDocument
		{
			Id = "r1",
			RequesterUserId = "auth0|you",
			RecipientUserId = "auth0|me",
			CreatedAt = DateTime.UtcNow
		};

		var requests = new Mock<IMongoCollection<FriendRequestDocument>>();
		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<FriendRequestDocument>)new[] { request });
		requests.Setup(c => c.FindAsync(
			It.IsAny<FilterDefinition<FriendRequestDocument>>(),
			It.IsAny<FindOptions<FriendRequestDocument, FriendRequestDocument>>(),
			It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
		requests.Setup(c => c.FindSync(
			It.IsAny<FilterDefinition<FriendRequestDocument>>(),
			It.IsAny<FindOptions<FriendRequestDocument, FriendRequestDocument>>(),
			It.IsAny<CancellationToken>())).Returns(cursor.Object);
		requests.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<FriendRequestDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new DeleteResult.Acknowledged(1));

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<FriendRequestDocument>("friend_requests", null)).Returns(requests.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new RejectFriendRequestCommandHandler(client.Object);
		var result = await handler.HandleAsync(new RejectFriendRequestCommand("auth0|me", "r1"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal("auth0|you", result.Value?.RequesterUserId);
	}
}
