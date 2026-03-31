using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Commands;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.MealPlans.Commands;

public class DismissSharedMealPlanTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenShareIdMissing()
	{
		var handler = new DismissSharedMealPlanCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new DismissSharedMealPlanCommand("recipient1", " "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenNoMatch()
	{
		var handler = new DismissSharedMealPlanCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new DismissSharedMealPlanCommand("recipient1", Guid.NewGuid().ToString()),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsSuccess_WhenMatched()
	{
		var id = Guid.NewGuid();
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.MealPlanShares.Add(new MealPlanShareEntity
			{
				Id = id,
				OwnerUserId = "owner1",
				SharedWithUserId = "recipient1",
				WeekStart = "2026-02-23",
				Permission = "ReadOnly",
				SharedAt = DateTime.UtcNow,
				DismissedByRecipient = false
			});
		});

		var handler = new DismissSharedMealPlanCommandHandler(context);
		var result = await handler.HandleAsync(
			new DismissSharedMealPlanCommand("recipient1", id.ToString()),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.True(context.MealPlanShares.Single().DismissedByRecipient);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new DismissSharedMealPlanCommandHandler(context);
		var result = await handler.HandleAsync(
			new DismissSharedMealPlanCommand("recipient1", Guid.NewGuid().ToString()),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
