using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.MealPlans.Queries;

public class GetSharedWithMeTests
{
	private static MealPlanEntity PlanDoc(string owner) => new()
	{
		Id = Guid.NewGuid(),
		UserId = owner,
		WeekStart = "2026-02-23",
		Days = [new DayPlanData { Day = "Monday", Slots = new Dictionary<string, List<MealSlotItemData>> { ["Breakfast"] = [], ["Lunch"] = [], ["Supper"] = [], ["Snacks"] = [] } }],
		CreatedAt = DateTime.UtcNow,
		UpdatedAt = DateTime.UtcNow
	};

	[Fact]
	public async Task HandleAsync_ReturnsEmpty_WhenNoShares()
	{
		var handler = new GetSharedWithMeQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetSharedWithMeQuery("recipient1", "2026-02-23"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Empty(result.Value!);
	}

	[Fact]
	public async Task HandleAsync_ReturnsSharedMealPlans_WhenDataExists()
	{
		var shareDoc = new MealPlanShareEntity
		{
			Id = Guid.NewGuid(),
			OwnerUserId = "owner1",
			SharedWithUserId = "recipient1",
			WeekStart = "2026-02-23",
			Permission = nameof(SharePermission.ReadWrite),
			SharedAt = DateTime.UtcNow,
			DismissedByRecipient = false
		};
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.MealPlanShares.Add(shareDoc);
			db.Users.Add(new UserEntity
			{
				Id = Guid.NewGuid(),
				AuthUserId = "owner1",
				Name = "Morgan",
				Email = "morgan@example.com",
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
			db.MealPlans.Add(PlanDoc("owner1"));
		});

		var handler = new GetSharedWithMeQueryHandler(context);
		var result = await handler.HandleAsync(new GetSharedWithMeQuery("recipient1", "2026-02-23"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!);
		Assert.Equal("Morgan", result.Value![0].OwnerName);
		Assert.Equal("morgan@example.com", result.Value![0].OwnerEmail);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new GetSharedWithMeQueryHandler(context);
		var result = await handler.HandleAsync(new GetSharedWithMeQuery("recipient1", "2026-02-23"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
