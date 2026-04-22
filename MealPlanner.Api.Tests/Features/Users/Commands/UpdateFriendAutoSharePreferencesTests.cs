using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

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
		var now = DateTime.UtcNow;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Friendships.Add(new FriendshipEntity
			{
				Id = Guid.NewGuid(),
				UserAId = "auth0|friend",
				UserBId = "auth0|me",
				CreatedAt = now
			});
		});

		var handler = new UpdateFriendAutoSharePreferencesCommandHandler(context);
		var result = await handler.HandleAsync(
			new UpdateFriendAutoSharePreferencesCommand("auth0|me", "auth0|friend", true, true),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.True(result.Value?.AutoShareMealPlans);
		Assert.True(result.Value?.AutoShareGroceryLists);
		Assert.Single(context.FriendAutoSharePreferences);
	}

	[Fact]
	public async Task HandleAsync_BackfillsCurrentWeekShares_WhenTogglesEnabled()
	{
		var now = DateTime.UtcNow;
		var weekStart = DateOnly.FromDateTime(DateTime.UtcNow);
		var offset = weekStart.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)weekStart.DayOfWeek - 1;
		var monday = weekStart.AddDays(-offset).ToString("yyyy-MM-dd");

		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Friendships.Add(new FriendshipEntity
			{
				Id = Guid.NewGuid(),
				UserAId = "auth0|friend",
				UserBId = "auth0|me",
				CreatedAt = now
			});

			db.MealPlans.Add(new MealPlanEntity
			{
				Id = Guid.NewGuid(),
				UserId = "auth0|me",
				WeekStart = monday,
				CreatedAt = now,
				UpdatedAt = now,
				Days = []
			});

			db.GroceryLists.Add(new GroceryListEntity
			{
				Id = Guid.NewGuid(),
				UserId = "auth0|me",
				WeekStart = monday,
				CreatedAt = now,
				UpdatedAt = now,
				Items = [],
				PantryStapleItems = []
			});
		});

		var handler = new UpdateFriendAutoSharePreferencesCommandHandler(context);
		var result = await handler.HandleAsync(
			new UpdateFriendAutoSharePreferencesCommand("auth0|me", "auth0|friend", true, true),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(context.MealPlanShares.Where(s => s.OwnerUserId == "auth0|me" && s.SharedWithUserId == "auth0|friend"));
		Assert.Single(context.GroceryListShares.Where(s => s.OwnerUserId == "auth0|me" && s.SharedWithUserId == "auth0|friend"));
	}
}
