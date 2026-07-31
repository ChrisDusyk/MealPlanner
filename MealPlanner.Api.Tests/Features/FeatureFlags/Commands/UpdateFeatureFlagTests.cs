using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.FeatureFlags;
using MealPlanner.Api.Features.FeatureFlags.Commands;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Tests.Features.FeatureFlags.Commands;

public class UpdateFeatureFlagTests
{
	private const string BooleanDefinition =
		"{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"on\"}";

	private static MealPlannerDbContext SeedFlag(out string databaseName)
	{
		databaseName = $"mealplanner-tests-{Guid.NewGuid():N}";
		return TestDbContextFactory.CreateContext(context =>
		{
			context.FeatureFlags.Add(new FeatureFlagEntity
			{
				Key = "demo-banner",
				Enabled = false,
				ValueType = FeatureFlagValueTypes.Boolean,
				DisabledVariant = null,
				DefinitionJson = BooleanDefinition,
				Description = "Original description.",
				UpdatedAt = DateTime.UtcNow.AddDays(-1)
			});
		}, databaseName);
	}

	private static UpdateFeatureFlagCommand Command(
		string key = "demo-banner",
		bool enabled = true,
		string valueType = FeatureFlagValueTypes.Boolean,
		string? disabledVariant = "off",
		string definitionJson = BooleanDefinition,
		string? description = "Updated description.") =>
		new(key, enabled, valueType, disabledVariant, definitionJson, description);

	[Fact]
	public async Task HandleAsync_UpdatesEveryEditableField()
	{
		var context = SeedFlag(out var databaseName);
		var handler = new UpdateFeatureFlagCommandHandler(context);

		var result = await handler.HandleAsync(Command(), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);

		using var verification = TestDbContextFactory.CreateContext(databaseName);
		var stored = await verification.FeatureFlags.SingleAsync(
			f => f.Key == "demo-banner", TestContext.Current.CancellationToken);

		Assert.True(stored.Enabled);
		Assert.Equal("off", stored.DisabledVariant);
		Assert.Equal("Updated description.", stored.Description);
	}

	[Fact]
	public async Task HandleAsync_ChangesTheValueType_WhenTheNewVariantsMatch()
	{
		var context = SeedFlag(out var databaseName);
		var handler = new UpdateFeatureFlagCommandHandler(context);

		var result = await handler.HandleAsync(
			Command(
				valueType: FeatureFlagValueTypes.String,
				disabledVariant: "control",
				definitionJson: "{\"variants\":{\"control\":\"a\",\"treatment\":\"b\"},\"defaultVariant\":\"treatment\"}"),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);

		using var verification = TestDbContextFactory.CreateContext(databaseName);
		var stored = await verification.FeatureFlags.SingleAsync(
			f => f.Key == "demo-banner", TestContext.Current.CancellationToken);

		Assert.Equal(FeatureFlagValueTypes.String, stored.ValueType);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenKeyIsBlank()
	{
		var handler = new UpdateFeatureFlagCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			Command(key: "  "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenTheDisabledVariantIsUnknown()
	{
		var context = SeedFlag(out _);
		var handler = new UpdateFeatureFlagCommandHandler(context);

		var result = await handler.HandleAsync(
			Command(disabledVariant: "nope"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenFlagDoesNotExist()
	{
		var handler = new UpdateFeatureFlagCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			Command(key: "missing-flag"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenTheContextIsDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		await context.DisposeAsync();

		var handler = new UpdateFeatureFlagCommandHandler(context);

		var result = await handler.HandleAsync(Command(), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
