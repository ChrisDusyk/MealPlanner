using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.FeatureFlags.Commands;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Tests.Features.FeatureFlags.Commands;

public class SetFeatureFlagEnabledTests
{
	private static MealPlannerDbContext SeedFlag(string key, bool enabled, out string databaseName)
	{
		databaseName = $"mealplanner-tests-{Guid.NewGuid():N}";
		return TestDbContextFactory.CreateContext(context =>
		{
			context.FeatureFlags.Add(new FeatureFlagEntity
			{
				Key = key,
				Enabled = enabled,
				DefinitionJson = "{\"defaultVariant\":\"on\"}",
				Description = null,
				UpdatedAt = DateTime.UtcNow.AddDays(-1)
			});
		}, databaseName);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenKeyIsBlank()
	{
		var handler = new SetFeatureFlagEnabledCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			new SetFeatureFlagEnabledCommand("  ", true), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenFlagDoesNotExist()
	{
		var handler = new SetFeatureFlagEnabledCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			new SetFeatureFlagEnabledCommand("missing", true), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_UpdatesEnabledState_AndTimestamp_WhenFlagExists()
	{
		var db = SeedFlag("demo-banner", enabled: false, out var databaseName);
		var handler = new SetFeatureFlagEnabledCommandHandler(db);
		var before = DateTime.UtcNow;

		var result = await handler.HandleAsync(
			new SetFeatureFlagEnabledCommand("demo-banner", true), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.True(result.Value!.Enabled);

		// Persisted to the database.
		await using var verifyDb = TestDbContextFactory.CreateContext(databaseName);
		var stored = await verifyDb.FeatureFlags.SingleAsync(f => f.Key == "demo-banner", TestContext.Current.CancellationToken);
		Assert.True(stored.Enabled);
		Assert.True(stored.UpdatedAt >= before);
	}
}
