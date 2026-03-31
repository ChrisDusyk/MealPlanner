using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
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
		var handler = new AcceptFriendRequestCommandHandler(TestDbContextFactory.CreateContext());
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
		Assert.Equal("auth0|you", result.Value?.RequesterUserId);
		friendships.Verify(c => c.InsertOneAsync(It.IsAny<FriendshipDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
		requests.Verify(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<FriendRequestDocument>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
	}

	[Fact]
	public async Task HandleAsync_UpsertsPreferences_AndBackfillsCurrentWeekShares_WhenEnabled()
	{
		var now = DateOnly.FromDateTime(DateTime.UtcNow);
		var offset = now.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)now.DayOfWeek - 1;
		var monday = now.AddDays(-offset).ToString("yyyy-MM-dd");

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

		var preferenceDocs = new[]
		{
			new FriendAutoSharePreferenceDocument
			{
				UserId = "auth0|you",
				FriendUserId = "auth0|me",
				AutoShareMealPlans = true,
				AutoShareGroceryLists = true
			},
			new FriendAutoSharePreferenceDocument
			{
				UserId = "auth0|me",
				FriendUserId = "auth0|you",
				AutoShareMealPlans = false,
				AutoShareGroceryLists = false
			}
		};
		var preferences = CreateCollectionWithFindResults<FriendAutoSharePreferenceDocument>(preferenceDocs);
		preferences.Setup(c => c.UpdateOneAsync(
				It.IsAny<FilterDefinition<FriendAutoSharePreferenceDocument>>(),
				It.IsAny<UpdateDefinition<FriendAutoSharePreferenceDocument>>(),
				It.IsAny<UpdateOptions>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

		var mealPlans = CreateCollectionWithFindResults<MealPlanDocument>(
			new[]
			{
				new MealPlanDocument
				{
					Id = "mp1",
					UserId = "auth0|you",
					WeekStart = monday,
					Days = [],
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				}
			});

		var groceryLists = CreateCollectionWithFindResults<GroceryListDocument>(
			new[]
			{
				new GroceryListDocument
				{
					Id = "gl1",
					UserId = "auth0|you",
					WeekStart = monday,
					Items = [],
					PantryStapleItems = [],
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				}
			});

		var mealPlanShares = CreateCollectionWithFindResults<MealPlanShareDocument>();
		mealPlanShares.Setup(c => c.InsertOneAsync(It.IsAny<MealPlanShareDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		var groceryListShares = CreateCollectionWithFindResults<GroceryListShareDocument>();
		groceryListShares.Setup(c => c.InsertOneAsync(It.IsAny<GroceryListShareDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<FriendRequestDocument>("friend_requests", null)).Returns(requests.Object);
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);
		database.Setup(d => d.GetCollection<FriendAutoSharePreferenceDocument>("friend_auto_share_preferences", null)).Returns(preferences.Object);
		database.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(mealPlans.Object);
		database.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(groceryLists.Object);
		database.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(mealPlanShares.Object);
		database.Setup(d => d.GetCollection<GroceryListShareDocument>("grocerylist_shares", null)).Returns(groceryListShares.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new AcceptFriendRequestCommandHandler(client.Object);
		var result = await handler.HandleAsync(new AcceptFriendRequestCommand("auth0|me", "r1"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		preferences.Verify(c => c.UpdateOneAsync(
			It.IsAny<FilterDefinition<FriendAutoSharePreferenceDocument>>(),
			It.IsAny<UpdateDefinition<FriendAutoSharePreferenceDocument>>(),
			It.IsAny<UpdateOptions>(),
			It.IsAny<CancellationToken>()), Times.Exactly(2));
		mealPlanShares.Verify(c => c.InsertOneAsync(
			It.IsAny<MealPlanShareDocument>(),
			It.IsAny<InsertOneOptions>(),
			It.IsAny<CancellationToken>()), Times.Once);
		groceryListShares.Verify(c => c.InsertOneAsync(
			It.IsAny<GroceryListShareDocument>(),
			It.IsAny<InsertOneOptions>(),
			It.IsAny<CancellationToken>()), Times.Once);
	}
}
