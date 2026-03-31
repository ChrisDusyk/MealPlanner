using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Commands;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.MealPlans.Commands;

public class RevokeMealPlanShareTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenShareIdMissing()
	{
		var handler = new RevokeMealPlanShareCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new RevokeMealPlanShareCommand("owner1", " "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenNothingDeleted()
	{
		var handler = new RevokeMealPlanShareCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new RevokeMealPlanShareCommand("owner1", Guid.NewGuid().ToString()),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsSuccess_WhenDeleted()
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
				SharedAt = DateTime.UtcNow
			});
		});

		var handler = new RevokeMealPlanShareCommandHandler(context);
		var result = await handler.HandleAsync(
			new RevokeMealPlanShareCommand("owner1", id.ToString()),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Empty(context.MealPlanShares);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new RevokeMealPlanShareCommandHandler(context);
		var result = await handler.HandleAsync(
			new RevokeMealPlanShareCommand("owner1", Guid.NewGuid().ToString()),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}