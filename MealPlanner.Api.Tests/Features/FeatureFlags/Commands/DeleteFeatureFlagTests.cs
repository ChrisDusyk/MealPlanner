using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.FeatureFlags;
using MealPlanner.Api.Features.FeatureFlags.Commands;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Tests.Features.FeatureFlags.Commands;

public class DeleteFeatureFlagTests
{
	private static MealPlannerDbContext SeedFlag(out string databaseName)
	{
		databaseName = $"mealplanner-tests-{Guid.NewGuid():N}";
		return TestDbContextFactory.CreateContext(context =>
		{
			context.FeatureFlags.Add(new FeatureFlagEntity
			{
				Key = "demo-banner",
				Enabled = true,
				ValueType = FeatureFlagValueTypes.Boolean,
				DefinitionJson = "{\"variants\":{\"on\":true},\"defaultVariant\":\"on\"}",
				UpdatedAt = DateTime.UtcNow
			});
		}, databaseName);
	}

	[Fact]
	public async Task HandleAsync_RemovesTheFlag()
	{
		var context = SeedFlag(out var databaseName);
		var handler = new DeleteFeatureFlagCommandHandler(context);

		var result = await handler.HandleAsync(
			new DeleteFeatureFlagCommand("demo-banner"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);

		using var verification = TestDbContextFactory.CreateContext(databaseName);
		Assert.False(await verification.FeatureFlags.AnyAsync(
			f => f.Key == "demo-banner", TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenKeyIsBlank()
	{
		var handler = new DeleteFeatureFlagCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			new DeleteFeatureFlagCommand("  "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenFlagDoesNotExist()
	{
		var handler = new DeleteFeatureFlagCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			new DeleteFeatureFlagCommand("missing-flag"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenTheContextIsDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		await context.DisposeAsync();

		var handler = new DeleteFeatureFlagCommandHandler(context);

		var result = await handler.HandleAsync(
			new DeleteFeatureFlagCommand("demo-banner"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
