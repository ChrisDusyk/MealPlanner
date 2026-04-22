using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Users.Commands;

public class AcceptFriendRequestTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenRecipientMissing()
	{
		var handler = new AcceptFriendRequestCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new AcceptFriendRequestCommand(" ", "11111111-1111-1111-1111-111111111111"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenRequestNotFound()
	{
		using var db = TestDbContextFactory.CreateContext();
		var handler = new AcceptFriendRequestCommandHandler(db);
		var result = await handler.HandleAsync(new AcceptFriendRequestCommand("auth0|me", "11111111-1111-1111-1111-111111111111"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_CreatesFriendshipAndDeletesRequest_WhenValid()
	{
		var requestId = Guid.Parse("44444444-4444-4444-4444-444444444444");
		using var db = TestDbContextFactory.CreateContext();
		db.FriendRequests.Add(new FriendRequestEntity
		{
			Id = requestId,
			RequesterUserId = "auth0|you",
			RecipientUserId = "auth0|me",
			CreatedAt = DateTime.UtcNow
		});
		await db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var handler = new AcceptFriendRequestCommandHandler(db);
		var result = await handler.HandleAsync(new AcceptFriendRequestCommand("auth0|me", requestId.ToString()), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal("auth0|you", result.Value?.RequesterUserId);
		Assert.Empty(db.FriendRequests);
		Assert.Single(db.Friendships);
	}

	[Fact]
	public async Task HandleAsync_UpsertsPreferences_AndBackfillsCurrentWeekShares_WhenEnabled()
	{
		var requestId = Guid.Parse("55555555-5555-5555-5555-555555555555");
		var now = DateOnly.FromDateTime(DateTime.UtcNow);
		var offset = now.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)now.DayOfWeek - 1;
		var monday = now.AddDays(-offset).ToString("yyyy-MM-dd");

		using var db = TestDbContextFactory.CreateContext();
		db.FriendRequests.Add(new FriendRequestEntity
		{
			Id = requestId,
			RequesterUserId = "auth0|you",
			RecipientUserId = "auth0|me",
			CreatedAt = DateTime.UtcNow
		});
		db.FriendAutoSharePreferences.AddRange(
			new FriendAutoSharePreferenceEntity
			{
				Id = Guid.NewGuid(),
				UserId = "auth0|you",
				FriendUserId = "auth0|me",
				AutoShareMealPlans = true,
				AutoShareGroceryLists = true,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			},
			new FriendAutoSharePreferenceEntity
			{
				Id = Guid.NewGuid(),
				UserId = "auth0|me",
				FriendUserId = "auth0|you",
				AutoShareMealPlans = false,
				AutoShareGroceryLists = false,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
		db.MealPlans.Add(new MealPlanEntity
		{
			Id = Guid.NewGuid(),
			UserId = "auth0|you",
			WeekStart = monday,
			Days = [],
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		});
		db.GroceryLists.Add(new GroceryListEntity
		{
			Id = Guid.NewGuid(),
			UserId = "auth0|you",
			WeekStart = monday,
			Items = [],
			PantryStapleItems = [],
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		});
		await db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var handler = new AcceptFriendRequestCommandHandler(db);
		var result = await handler.HandleAsync(new AcceptFriendRequestCommand("auth0|me", requestId.ToString()), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Contains(db.MealPlanShares, s => s.OwnerUserId == "auth0|you" && s.SharedWithUserId == "auth0|me" && s.WeekStart == monday);
		Assert.Contains(db.GroceryListShares, s => s.OwnerUserId == "auth0|you" && s.SharedWithUserId == "auth0|me" && s.WeekStart == monday);
	}
}
