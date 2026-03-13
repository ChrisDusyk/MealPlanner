using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.Users.Commands;

public class AcceptFriendRequestTests
{
	private static Mock<IMongoCollection<T>> CreateCollectionWithFindResults<T>(params IReadOnlyCollection<T>[] results)
	{
		var collection = new Mock<IMongoCollection<T>>();
		if (results.Length == 0)
		{
			var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<T>)Array.Empty<T>());
			collection.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<T>>(), It.IsAny<FindOptions<T, T>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
			collection.Setup(c => c.FindSync(It.IsAny<FilterDefinition<T>>(), It.IsAny<FindOptions<T, T>>(), It.IsAny<CancellationToken>())).Returns(cursor.Object);
			return collection;
		}

		var asyncSetup = collection.SetupSequence(c => c.FindAsync(It.IsAny<FilterDefinition<T>>(), It.IsAny<FindOptions<T, T>>(), It.IsAny<CancellationToken>()));
		var syncSetup = collection.SetupSequence(c => c.FindSync(It.IsAny<FilterDefinition<T>>(), It.IsAny<FindOptions<T, T>>(), It.IsAny<CancellationToken>()));
		foreach (var result in results)
		{
			var cursor = MongoTestHelpers.CreateCursor(result);
			asyncSetup = asyncSetup.ReturnsAsync(cursor.Object);
			syncSetup = syncSetup.Returns(cursor.Object);
		}

		return collection;
	}

	private static void AttachIndexManager<T>(Mock<IMongoCollection<T>> collection) where T : class
	{
		var indexManager = new Mock<IMongoIndexManager<T>>();
		indexManager.Setup(i => i.CreateOneAsync(
			It.IsAny<CreateIndexModel<T>>(),
			It.IsAny<CreateOneIndexOptions>(),
			It.IsAny<CancellationToken>())).ReturnsAsync("idx");
		collection.SetupGet(c => c.Indexes).Returns(indexManager.Object);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenRecipientMissing()
	{
		var handler = new AcceptFriendRequestCommandHandler(new Mock<IMongoClient>().Object);
		var result = await handler.HandleAsync(new AcceptFriendRequestCommand(" ", "r1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenRequestNotFound()
	{
		var requests = CreateCollectionWithFindResults<FriendRequestDocument>(Array.Empty<FriendRequestDocument>());
		var friendships = CreateCollectionWithFindResults<FriendshipDocument>();
		AttachIndexManager(requests);
		AttachIndexManager(friendships);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<FriendRequestDocument>("friend_requests", null)).Returns(requests.Object);
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new AcceptFriendRequestCommandHandler(client.Object);
		var result = await handler.HandleAsync(new AcceptFriendRequestCommand("auth0|me", "r1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_CreatesFriendshipAndDeletesRequest_WhenValid()
	{
		var request = new FriendRequestDocument
		{
			Id = "r1",
			RequesterUserId = "auth0|you",
			RecipientUserId = "auth0|me",
			CreatedAt = DateTime.UtcNow
		};

		var requests = CreateCollectionWithFindResults<FriendRequestDocument>(new[] { request });
		requests.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<FriendRequestDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new DeleteResult.Acknowledged(1));
		var friendships = CreateCollectionWithFindResults<FriendshipDocument>(Array.Empty<FriendshipDocument>());
		friendships.Setup(c => c.InsertOneAsync(It.IsAny<FriendshipDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);
		AttachIndexManager(requests);
		AttachIndexManager(friendships);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<FriendRequestDocument>("friend_requests", null)).Returns(requests.Object);
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new AcceptFriendRequestCommandHandler(client.Object);
		var result = await handler.HandleAsync(new AcceptFriendRequestCommand("auth0|me", "r1"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		friendships.Verify(c => c.InsertOneAsync(It.IsAny<FriendshipDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
		requests.Verify(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<FriendRequestDocument>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
	}
}
