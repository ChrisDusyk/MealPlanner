using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.MealPlans.Queries;

public class GetSharesForMealPlanTests
{
	[Fact]
	public async Task HandleAsync_ReturnsEmpty_WhenNoShares()
	{
		var handler = new GetSharesForMealPlanQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetSharesForMealPlanQuery("owner1", "2026-02-23"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Empty(result.Value!);
	}

	[Fact]
	public async Task HandleAsync_ReturnsEnrichedShares_WhenFound()
	{
		var shareDoc = new MealPlanShareEntity
		{
			Id = Guid.NewGuid(),
			OwnerUserId = "owner1",
			SharedWithUserId = "recipient1",
			WeekStart = "2026-02-23",
			Permission = nameof(SharePermission.ReadOnly),
			SharedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
			DismissedByRecipient = false
		};
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.MealPlanShares.Add(shareDoc);
			db.Users.Add(new UserEntity
			{
				Id = Guid.NewGuid(),
				AuthUserId = "recipient1",
				Name = "Alex",
				Email = "alex@example.com",
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
		});

		var handler = new GetSharesForMealPlanQueryHandler(context);
		var result = await handler.HandleAsync(new GetSharesForMealPlanQuery("owner1", "2026-02-23"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!);
		Assert.Equal("Alex", result.Value![0].RecipientName);
		Assert.Equal("alex@example.com", result.Value![0].RecipientEmail);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new GetSharesForMealPlanQueryHandler(context);
		var result = await handler.HandleAsync(new GetSharesForMealPlanQuery("owner1", "2026-02-23"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
