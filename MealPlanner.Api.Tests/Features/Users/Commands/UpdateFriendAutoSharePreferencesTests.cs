using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.Users.Commands;

public class UpdateFriendAutoSharePreferencesTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenUserIdMissing()
	{
		var handler = new UpdateFriendAutoSharePreferencesCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			new UpdateFriendAutoSharePreferencesCommand(" ", "auth0|friend", true, false),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenFriendshipMissing()
	{
		var emptyFriendshipsCursor = MongoTestHelpers.CreateCursor(Array.Empty<FriendshipDocument>());
		var friendships = new Mock<IMongoCollection<FriendshipDocument>>();
		friendships.Setup(c => c.FindAsync(
				It.IsAny<FilterDefinition<FriendshipDocument>>(),
				It.IsAny<FindOptions<FriendshipDocument, FriendshipDocument>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(emptyFriendshipsCursor.Object);
		friendships.Setup(c => c.FindSync(
				It.IsAny<FilterDefinition<FriendshipDocument>>(),
				It.IsAny<FindOptions<FriendshipDocument, FriendshipDocument>>(),
				It.IsAny<CancellationToken>()))
			.Returns(emptyFriendshipsCursor.Object);

		var preferences = new Mock<IMongoCollection<FriendAutoSharePreferenceDocument>>();

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);
		database.Setup(d => d.GetCollection<FriendAutoSharePreferenceDocument>("friend_auto_share_preferences", null))
			.Returns(preferences.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new UpdateFriendAutoSharePreferencesCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new UpdateFriendAutoSharePreferencesCommand("auth0|me", "auth0|friend", true, false),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_UpsertsPreferences_WhenFriendshipExists()
	{
		var friendship = new FriendshipDocument
		{
			UserAId = "auth0|friend",
			UserBId = "auth0|me",
			CreatedAt = DateTime.UtcNow
		};
		var friendshipsCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<FriendshipDocument>)new[] { friendship });
		var friendships = new Mock<IMongoCollection<FriendshipDocument>>();
		friendships.Setup(c => c.FindAsync(
				It.IsAny<FilterDefinition<FriendshipDocument>>(),
				It.IsAny<FindOptions<FriendshipDocument, FriendshipDocument>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(friendshipsCursor.Object);
		friendships.Setup(c => c.FindSync(
				It.IsAny<FilterDefinition<FriendshipDocument>>(),
				It.IsAny<FindOptions<FriendshipDocument, FriendshipDocument>>(),
				It.IsAny<CancellationToken>()))
			.Returns(friendshipsCursor.Object);

		var preferences = new Mock<IMongoCollection<FriendAutoSharePreferenceDocument>>();
		preferences.Setup(c => c.UpdateOneAsync(
				It.IsAny<FilterDefinition<FriendAutoSharePreferenceDocument>>(),
				It.IsAny<UpdateDefinition<FriendAutoSharePreferenceDocument>>(),
				It.IsAny<UpdateOptions>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);
		database.Setup(d => d.GetCollection<FriendAutoSharePreferenceDocument>("friend_auto_share_preferences", null))
			.Returns(preferences.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new UpdateFriendAutoSharePreferencesCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new UpdateFriendAutoSharePreferencesCommand("auth0|me", "auth0|friend", true, true),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.True(result.Value?.AutoShareMealPlans);
		Assert.True(result.Value?.AutoShareGroceryLists);
		preferences.Verify(c => c.UpdateOneAsync(
			It.IsAny<FilterDefinition<FriendAutoSharePreferenceDocument>>(),
			It.IsAny<UpdateDefinition<FriendAutoSharePreferenceDocument>>(),
			It.IsAny<UpdateOptions>(),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task HandleAsync_BackfillsCurrentWeekShares_WhenTogglesEnabled()
	{
		var friendship = new FriendshipDocument
		{
			UserAId = "auth0|friend",
			UserBId = "auth0|me",
			CreatedAt = DateTime.UtcNow
		};
		var friendshipsCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<FriendshipDocument>)new[] { friendship });
		var friendships = new Mock<IMongoCollection<FriendshipDocument>>();
		friendships.Setup(c => c.FindAsync(
				It.IsAny<FilterDefinition<FriendshipDocument>>(),
				It.IsAny<FindOptions<FriendshipDocument, FriendshipDocument>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(friendshipsCursor.Object);
		friendships.Setup(c => c.FindSync(
				It.IsAny<FilterDefinition<FriendshipDocument>>(),
				It.IsAny<FindOptions<FriendshipDocument, FriendshipDocument>>(),
				It.IsAny<CancellationToken>()))
			.Returns(friendshipsCursor.Object);

		var preferences = new Mock<IMongoCollection<FriendAutoSharePreferenceDocument>>();
		preferences.Setup(c => c.UpdateOneAsync(
				It.IsAny<FilterDefinition<FriendAutoSharePreferenceDocument>>(),
				It.IsAny<UpdateDefinition<FriendAutoSharePreferenceDocument>>(),
				It.IsAny<UpdateOptions>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(new UpdateResult.Acknowledged(1, 1, null));

		var weekStart = DateOnly.FromDateTime(DateTime.UtcNow);
		var offset = weekStart.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)weekStart.DayOfWeek - 1;
		var monday = weekStart.AddDays(-offset).ToString("yyyy-MM-dd");

		var mealPlans = new Mock<IMongoCollection<MealPlanDocument>>();
		var mealPlanCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanDocument>)new[]
		{
			new MealPlanDocument
			{
				Id = "mp1",
				UserId = "auth0|me",
				WeekStart = monday,
				Days = [],
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			}
		});
		mealPlans.Setup(c => c.FindAsync(
				It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(mealPlanCursor.Object);
		mealPlans.Setup(c => c.FindSync(
				It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(),
				It.IsAny<CancellationToken>()))
			.Returns(mealPlanCursor.Object);

		var groceryLists = new Mock<IMongoCollection<GroceryListDocument>>();
		var groceryListCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<GroceryListDocument>)new[]
		{
			new GroceryListDocument
			{
				Id = "gl1",
				UserId = "auth0|me",
				WeekStart = monday,
				Items = [],
				PantryStapleItems = [],
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			}
		});
		groceryLists.Setup(c => c.FindAsync(
				It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(groceryListCursor.Object);
		groceryLists.Setup(c => c.FindSync(
				It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(),
				It.IsAny<CancellationToken>()))
			.Returns(groceryListCursor.Object);

		var mealPlanShares = new Mock<IMongoCollection<MealPlanShareDocument>>();
		mealPlanShares.Setup(c => c.InsertOneAsync(
				It.IsAny<MealPlanShareDocument>(),
				It.IsAny<InsertOneOptions>(),
				It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		var groceryListShares = new Mock<IMongoCollection<GroceryListShareDocument>>();
		groceryListShares.Setup(c => c.InsertOneAsync(
				It.IsAny<GroceryListShareDocument>(),
				It.IsAny<InsertOneOptions>(),
				It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);
		database.Setup(d => d.GetCollection<FriendAutoSharePreferenceDocument>("friend_auto_share_preferences", null))
			.Returns(preferences.Object);
		database.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(mealPlans.Object);
		database.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(groceryLists.Object);
		database.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(mealPlanShares.Object);
		database.Setup(d => d.GetCollection<GroceryListShareDocument>("grocerylist_shares", null))
			.Returns(groceryListShares.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new UpdateFriendAutoSharePreferencesCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new UpdateFriendAutoSharePreferencesCommand("auth0|me", "auth0|friend", true, true),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
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
