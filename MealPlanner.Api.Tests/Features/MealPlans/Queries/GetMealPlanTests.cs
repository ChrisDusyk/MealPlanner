using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.MealPlans.Queries;

public class GetMealPlanTests
{
	[Fact]
	public async Task HandleAsync_ReturnsExistingPlan_WhenFound()
	{
		var id = Guid.NewGuid();
		var now = DateTime.UtcNow;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.MealPlans.Add(new MealPlanEntity
			{
				Id = id,
				UserId = "u1",
				WeekStart = "2026-02-23",
				Days =
				[
					new DayPlanData
					{
						Day = "Monday",
						Slots = new Dictionary<string, List<MealSlotItemData>>
						{
							["Breakfast"] = [new MealSlotItemData { RecipeId = Guid.NewGuid().ToString(), Name = "Oats" }],
							["Lunch"] = [],
							["Supper"] = [],
							["Snacks"] = []
						}
					}
				],
				CreatedAt = now,
				UpdatedAt = now
			});
		});

		var handler = new GetMealPlanQueryHandler(context);
		var result = await handler.HandleAsync(new GetMealPlanQuery("u1", new DateOnly(2026, 2, 25)), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal(id.ToString(), result.Value!.Id);
		Assert.Equal(new DateOnly(2026, 2, 23), result.Value.WeekStart);
	}

	[Fact]
	public async Task HandleAsync_CreatesEmptyPlan_WhenMissing()
	{
		var context = TestDbContextFactory.CreateContext();
		var handler = new GetMealPlanQueryHandler(context);
		var result = await handler.HandleAsync(new GetMealPlanQuery("u1", new DateOnly(2026, 2, 26)), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal("2026-02-23", context.MealPlans.Single().WeekStart);
		Assert.Equal(7, context.MealPlans.Single().Days.Count);
	}

	[Fact]
	public async Task HandleAsync_CreatesShares_WhenAutoSharePreferenceEnabledForActiveFriend()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.FriendAutoSharePreferences.Add(new FriendAutoSharePreferenceEntity
			{
				Id = Guid.NewGuid(),
				UserId = "u1",
				FriendUserId = "u2",
				AutoShareMealPlans = true,
				AutoShareGroceryLists = false,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});

			db.Friendships.Add(new FriendshipEntity
			{
				Id = Guid.NewGuid(),
				UserAId = "u1",
				UserBId = "u2",
				CreatedAt = DateTime.UtcNow
			});
		});

		var handler = new GetMealPlanQueryHandler(context);
		var result = await handler.HandleAsync(new GetMealPlanQuery("u1", new DateOnly(2026, 2, 26)), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(context.MealPlanShares.Where(s => s.SharedWithUserId == "u2"));
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new GetMealPlanQueryHandler(context);
		var result = await handler.HandleAsync(new GetMealPlanQuery("u1", new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
