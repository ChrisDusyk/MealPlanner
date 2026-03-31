using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Features.Users.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.Users.Queries;

public class GetFriendsForUserTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenUserMissing()
	{
		var handler = new GetFriendsForUserQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetFriendsForUserQuery(" "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsFriendSummaries_WhenMatchesFound()
	{
		var friendship = new FriendshipDocument
		{
			UserAId = "auth0|me",
			UserBId = "auth0|you",
			CreatedAt = DateTime.UtcNow
		};
		var friendshipsCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<FriendshipDocument>)new[] { friendship });
		var friendships = new Mock<IMongoCollection<FriendshipDocument>>();
		friendships.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<FriendshipDocument>>(), It.IsAny<FindOptions<FriendshipDocument, FriendshipDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(friendshipsCursor.Object);
		friendships.Setup(c => c.FindSync(It.IsAny<FilterDefinition<FriendshipDocument>>(), It.IsAny<FindOptions<FriendshipDocument, FriendshipDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(friendshipsCursor.Object);

		var friendDoc = new UserDocument { Auth0UserId = "auth0|you", Name = "You", Email = "you@example.com" };
		var usersCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<UserDocument>)new[] { friendDoc });
		var users = new Mock<IMongoCollection<UserDocument>>();
		users.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<UserDocument>>(), It.IsAny<FindOptions<UserDocument, UserDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(usersCursor.Object);
		users.Setup(c => c.FindSync(It.IsAny<FilterDefinition<UserDocument>>(), It.IsAny<FindOptions<UserDocument, UserDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(usersCursor.Object);

		var preferenceDoc = new FriendAutoSharePreferenceDocument
		{
			UserId = "auth0|me",
			FriendUserId = "auth0|you",
			AutoShareMealPlans = true,
			AutoShareGroceryLists = false
		};
		var preferencesCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<FriendAutoSharePreferenceDocument>)new[] { preferenceDoc });
		var preferences = new Mock<IMongoCollection<FriendAutoSharePreferenceDocument>>();
		preferences.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<FriendAutoSharePreferenceDocument>>(), It.IsAny<FindOptions<FriendAutoSharePreferenceDocument, FriendAutoSharePreferenceDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(preferencesCursor.Object);
		preferences.Setup(c => c.FindSync(It.IsAny<FilterDefinition<FriendAutoSharePreferenceDocument>>(), It.IsAny<FindOptions<FriendAutoSharePreferenceDocument, FriendAutoSharePreferenceDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(preferencesCursor.Object);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);
		database.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(users.Object);
		database.Setup(d => d.GetCollection<FriendAutoSharePreferenceDocument>("friend_auto_share_preferences", null)).Returns(preferences.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new GetFriendsForUserQueryHandler(client.Object);
		var result = await handler.HandleAsync(new GetFriendsForUserQuery("auth0|me"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!);
		Assert.Equal("auth0|you", result.Value![0].UserId);
		Assert.True(result.Value[0].AutoShareMealPlans);
		Assert.False(result.Value[0].AutoShareGroceryLists);
	}
}
