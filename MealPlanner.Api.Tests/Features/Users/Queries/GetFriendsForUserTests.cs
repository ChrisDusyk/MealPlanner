using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

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
		var now = DateTime.UtcNow;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Friendships.Add(new FriendshipEntity
			{
				Id = Guid.NewGuid(),
				UserAId = "auth0|me",
				UserBId = "auth0|you",
				CreatedAt = now
			});

			db.Users.Add(new UserEntity
			{
				Id = Guid.NewGuid(),
				AuthUserId = "auth0|you",
				Name = "You",
				Email = "you@example.com",
				CreatedAt = now,
				UpdatedAt = now
			});

			db.FriendAutoSharePreferences.Add(new FriendAutoSharePreferenceEntity
			{
				Id = Guid.NewGuid(),
				UserId = "auth0|me",
				FriendUserId = "auth0|you",
				AutoShareMealPlans = true,
				AutoShareGroceryLists = false,
				CreatedAt = now,
				UpdatedAt = now
			});
		});

		var handler = new GetFriendsForUserQueryHandler(context);
		var result = await handler.HandleAsync(new GetFriendsForUserQuery("auth0|me"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!);
		Assert.Equal("auth0|you", result.Value![0].UserId);
		Assert.True(result.Value[0].AutoShareMealPlans);
		Assert.False(result.Value[0].AutoShareGroceryLists);
	}
}
