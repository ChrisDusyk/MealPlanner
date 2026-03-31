using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using System.Runtime.CompilerServices;

namespace MealPlanner.Api.Tests.Features.Users.Commands;

public class SendFriendRequestByEmailTests
{
	private static ConnectionId CreateConnectionId() =>
		new(new ServerId(new ClusterId(), new System.Net.DnsEndPoint("localhost", 27017)));

	private static MongoWriteException CreateDuplicateKeyWriteException()
	{
		var writeError = (WriteError)RuntimeHelpers.GetUninitializedObject(typeof(WriteError));
		typeof(WriteError).GetField("_category", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.SetValue(writeError, ServerErrorCategory.DuplicateKey);
		typeof(WriteError).GetField("_code", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.SetValue(writeError, 11000);
		typeof(WriteError).GetField("_message", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.SetValue(writeError, "E11000 duplicate key error");
		typeof(WriteError).GetField("_details", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.SetValue(writeError, new BsonDocument());

		return new MongoWriteException(CreateConnectionId(), writeError, null, null);
	}

	private static Mock<IMongoCollection<T>> CreateCollectionWithFindResults<T>(params IReadOnlyCollection<T>[] results)
	{
		var collection = new Mock<IMongoCollection<T>>();
		var asyncSetup = collection.SetupSequence(c => c.FindAsync(
			It.IsAny<FilterDefinition<T>>(),
			It.IsAny<FindOptions<T, T>>(),
			It.IsAny<CancellationToken>()));
		var syncSetup = collection.SetupSequence(c => c.FindSync(
			It.IsAny<FilterDefinition<T>>(),
			It.IsAny<FindOptions<T, T>>(),
			It.IsAny<CancellationToken>()));

		foreach (var result in results)
		{
			var cursor = MongoTestHelpers.CreateCursor(result);
			asyncSetup = asyncSetup.ReturnsAsync(cursor.Object);
			syncSetup = syncSetup.Returns(cursor.Object);
		}

		if (results.Length == 0)
		{
			var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<T>)Array.Empty<T>());
			collection.Setup(c => c.FindAsync(
				It.IsAny<FilterDefinition<T>>(),
				It.IsAny<FindOptions<T, T>>(),
				It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
			collection.Setup(c => c.FindSync(
				It.IsAny<FilterDefinition<T>>(),
				It.IsAny<FindOptions<T, T>>(),
				It.IsAny<CancellationToken>())).Returns(cursor.Object);
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
	public async Task HandleAsync_ReturnsValidationFailure_WhenRequesterMissing()
	{
		var handler = new SendFriendRequestByEmailCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand(" ", "pal@example.com"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenEmailMissing()
	{
		var handler = new SendFriendRequestByEmailCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand("auth0|me", " "),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenRequesterUserMissing()
	{
		var users = CreateCollectionWithFindResults<UserDocument>(Array.Empty<UserDocument>());
		var requests = CreateCollectionWithFindResults<FriendRequestDocument>();
		var friendships = CreateCollectionWithFindResults<FriendshipDocument>();
		AttachIndexManager(requests);
		AttachIndexManager(friendships);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		database.Setup(d => d.GetCollection<FriendRequestDocument>("friend_requests", null)).Returns(requests.Object);
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new SendFriendRequestByEmailCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand("auth0|missing", "pal@example.com"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenRecipientEmailNotFound()
	{
		var requester = new UserDocument { Auth0UserId = "auth0|me", Name = "Me", Email = "me@example.com" };
		var users = CreateCollectionWithFindResults<UserDocument>(
			new[] { requester },
			Array.Empty<UserDocument>());
		var requests = CreateCollectionWithFindResults<FriendRequestDocument>();
		var friendships = CreateCollectionWithFindResults<FriendshipDocument>();
		AttachIndexManager(requests);
		AttachIndexManager(friendships);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		database.Setup(d => d.GetCollection<FriendRequestDocument>("friend_requests", null)).Returns(requests.Object);
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new SendFriendRequestByEmailCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand("auth0|me", "missing@example.com"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenAlreadyFriends()
	{
		var requester = new UserDocument { Auth0UserId = "auth0|me", Name = "Me", Email = "me@example.com" };
		var recipient = new UserDocument { Auth0UserId = "auth0|you", Name = "You", Email = "you@example.com" };
		var users = CreateCollectionWithFindResults<UserDocument>(
			new[] { requester },
			new[] { recipient });
		var friendships = CreateCollectionWithFindResults<FriendshipDocument>(
			new[] { new FriendshipDocument { UserAId = "auth0|me", UserBId = "auth0|you", CreatedAt = DateTime.UtcNow } });
		var requests = CreateCollectionWithFindResults<FriendRequestDocument>();
		AttachIndexManager(requests);
		AttachIndexManager(friendships);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		database.Setup(d => d.GetCollection<FriendRequestDocument>("friend_requests", null)).Returns(requests.Object);
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new SendFriendRequestByEmailCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand("auth0|me", "you@example.com"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenOutgoingRequestAlreadyExists()
	{
		var requester = new UserDocument { Auth0UserId = "auth0|me", Name = "Me", Email = "me@example.com" };
		var recipient = new UserDocument { Auth0UserId = "auth0|you", Name = "You", Email = "you@example.com" };
		var users = CreateCollectionWithFindResults<UserDocument>(
			new[] { requester },
			new[] { recipient });
		var friendships = CreateCollectionWithFindResults<FriendshipDocument>(Array.Empty<FriendshipDocument>());
		var requests = CreateCollectionWithFindResults<FriendRequestDocument>(
			new[] { new FriendRequestDocument { Id = "r1", RequesterUserId = "auth0|me", RecipientUserId = "auth0|you", CreatedAt = DateTime.UtcNow } });
		AttachIndexManager(requests);
		AttachIndexManager(friendships);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		database.Setup(d => d.GetCollection<FriendRequestDocument>("friend_requests", null)).Returns(requests.Object);
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new SendFriendRequestByEmailCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand("auth0|me", "you@example.com"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_AutoAccepts_WhenReciprocalRequestExists()
	{
		var requester = new UserDocument { Auth0UserId = "auth0|me", Name = "Me", Email = "me@example.com" };
		var recipient = new UserDocument { Auth0UserId = "auth0|you", Name = "You", Email = "you@example.com" };
		var users = CreateCollectionWithFindResults<UserDocument>(
			new[] { requester },
			new[] { recipient });
		var friendships = CreateCollectionWithFindResults<FriendshipDocument>(Array.Empty<FriendshipDocument>());
		AttachIndexManager(friendships);
		friendships.Setup(c => c.InsertOneAsync(
			It.IsAny<FriendshipDocument>(),
			It.IsAny<InsertOneOptions>(),
			It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

		var reciprocal = new FriendRequestDocument
		{
			Id = "r2",
			RequesterUserId = "auth0|you",
			RecipientUserId = "auth0|me",
			CreatedAt = DateTime.UtcNow
		};
		var requests = CreateCollectionWithFindResults<FriendRequestDocument>(
			Array.Empty<FriendRequestDocument>(),
			new[] { reciprocal });
		AttachIndexManager(requests);
		requests.Setup(c => c.DeleteOneAsync(
			It.IsAny<FilterDefinition<FriendRequestDocument>>(),
			It.IsAny<CancellationToken>())).ReturnsAsync(new DeleteResult.Acknowledged(1));

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		database.Setup(d => d.GetCollection<FriendRequestDocument>("friend_requests", null)).Returns(requests.Object);
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new SendFriendRequestByEmailCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand("auth0|me", "you@example.com"),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal(SendFriendRequestStatus.Accepted, result.Value?.Status);
		requests.Verify(c => c.DeleteOneAsync(
			It.IsAny<FilterDefinition<FriendRequestDocument>>(),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task HandleAsync_CreatesPendingRequest_WhenNoReciprocalExists()
	{
		var requester = new UserDocument { Auth0UserId = "auth0|me", Name = "Me", Email = "me@example.com" };
		var recipient = new UserDocument { Auth0UserId = "auth0|you", Name = "You", Email = "you@example.com" };
		var users = CreateCollectionWithFindResults<UserDocument>(
			new[] { requester },
			new[] { recipient });
		var friendships = CreateCollectionWithFindResults<FriendshipDocument>(Array.Empty<FriendshipDocument>());
		AttachIndexManager(friendships);
		var requests = CreateCollectionWithFindResults<FriendRequestDocument>(
			Array.Empty<FriendRequestDocument>(),
			Array.Empty<FriendRequestDocument>());
		AttachIndexManager(requests);

		FriendRequestDocument? inserted = null;
		requests.Setup(c => c.InsertOneAsync(
			It.IsAny<FriendRequestDocument>(),
			It.IsAny<InsertOneOptions>(),
			It.IsAny<CancellationToken>()))
			.Callback<FriendRequestDocument, InsertOneOptions, CancellationToken>((doc, _, _) => inserted = doc)
			.Returns(Task.CompletedTask);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		database.Setup(d => d.GetCollection<FriendRequestDocument>("friend_requests", null)).Returns(requests.Object);
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new SendFriendRequestByEmailCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand("auth0|me", "you@example.com"),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal(SendFriendRequestStatus.Pending, result.Value?.Status);
		Assert.NotNull(inserted);
		Assert.Equal("auth0|me", inserted!.RequesterUserId);
		Assert.Equal("auth0|you", inserted.RecipientUserId);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenInsertHitsDuplicateKey()
	{
		var requester = new UserDocument { Auth0UserId = "auth0|me", Name = "Me", Email = "me@example.com" };
		var recipient = new UserDocument { Auth0UserId = "auth0|you", Name = "You", Email = "you@example.com" };
		var users = CreateCollectionWithFindResults<UserDocument>(
			new[] { requester },
			new[] { recipient });
		var friendships = CreateCollectionWithFindResults<FriendshipDocument>(Array.Empty<FriendshipDocument>());
		AttachIndexManager(friendships);
		var requests = CreateCollectionWithFindResults<FriendRequestDocument>(
			Array.Empty<FriendRequestDocument>(),
			Array.Empty<FriendRequestDocument>());
		AttachIndexManager(requests);
		requests.Setup(c => c.InsertOneAsync(
			It.IsAny<FriendRequestDocument>(),
			It.IsAny<InsertOneOptions>(),
			It.IsAny<CancellationToken>())).ThrowsAsync(CreateDuplicateKeyWriteException());

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		database.Setup(d => d.GetCollection<FriendRequestDocument>("friend_requests", null)).Returns(requests.Object);
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new SendFriendRequestByEmailCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand("auth0|me", "you@example.com"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}
}
