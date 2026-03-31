using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Commands;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.MealPlans.Commands;

public class ShareMealPlanTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenEmailMissing()
	{
		var handler = new ShareMealPlanCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new ShareMealPlanCommand("owner1", " ", "2026-02-23", SharePermission.ReadOnly),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenOwnerMissing()
	{
		var handler = new ShareMealPlanCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new ShareMealPlanCommand("owner1", "a@example.com", "2026-02-23", SharePermission.ReadOnly),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenSelfShare()
	{
		var now = DateTime.UtcNow;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Users.Add(new UserEntity
			{
				Id = Guid.NewGuid(),
				Auth0UserId = "owner1",
				Name = "Owner",
				Email = "owner@example.com",
				CreatedAt = now,
				UpdatedAt = now
			});
		});

		var handler = new ShareMealPlanCommandHandler(context);
		var result = await handler.HandleAsync(
			new ShareMealPlanCommand("owner1", "owner@example.com", "2026-02-23", SharePermission.ReadOnly),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenShareAlreadyExists()
	{
		var now = DateTime.UtcNow;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Users.AddRange(
				new UserEntity
				{
					Id = Guid.NewGuid(),
					Auth0UserId = "owner1",
					Name = "Owner",
					Email = "owner@example.com",
					CreatedAt = now,
					UpdatedAt = now
				},
				new UserEntity
				{
					Id = Guid.NewGuid(),
					Auth0UserId = "recipient1",
					Name = "Recipient",
					Email = "recipient@example.com",
					CreatedAt = now,
					UpdatedAt = now
				});

			db.MealPlanShares.Add(new MealPlanShareEntity
			{
				Id = Guid.NewGuid(),
				OwnerUserId = "owner1",
				SharedWithUserId = "recipient1",
				WeekStart = "2026-02-23",
				Permission = nameof(SharePermission.ReadOnly),
				SharedAt = now
			});
		});

		var handler = new ShareMealPlanCommandHandler(context);
		var result = await handler.HandleAsync(
			new ShareMealPlanCommand("owner1", "recipient@example.com", "2026-02-23", SharePermission.ReadOnly),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_CreatesShare_WhenValid()
	{
		var now = DateTime.UtcNow;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Users.AddRange(
				new UserEntity
				{
					Id = Guid.NewGuid(),
					Auth0UserId = "owner1",
					Name = "Owner",
					Email = "owner@example.com",
					CreatedAt = now,
					UpdatedAt = now
				},
				new UserEntity
				{
					Id = Guid.NewGuid(),
					Auth0UserId = "recipient1",
					Name = "Recipient",
					Email = "recipient@example.com",
					CreatedAt = now,
					UpdatedAt = now
				});
		});

		var handler = new ShareMealPlanCommandHandler(context);
		var result = await handler.HandleAsync(
			new ShareMealPlanCommand("owner1", "recipient@example.com", "2026-02-23", SharePermission.ReadWrite),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(context.MealPlanShares);
		Assert.Equal("owner1", context.MealPlanShares.Single().OwnerUserId);
		Assert.Equal("recipient1", context.MealPlanShares.Single().SharedWithUserId);
		Assert.Equal(nameof(SharePermission.ReadWrite), context.MealPlanShares.Single().Permission);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new ShareMealPlanCommandHandler(context);
		var result = await handler.HandleAsync(
			new ShareMealPlanCommand("owner1", "recipient@example.com", "2026-02-23", SharePermission.ReadOnly),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}