using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.FeatureFlags;
using MealPlanner.Api.Features.FeatureFlags.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.FeatureFlags.Queries;

public class GetFeatureFlagByKeyTests
{
	[Fact]
	public async Task HandleAsync_ReturnsTheFlag_WhenItExists()
	{
		var context = TestDbContextFactory.CreateContext(db =>
		{
			db.FeatureFlags.Add(new FeatureFlagEntity
			{
				Key = "demo-banner",
				Enabled = true,
				ValueType = FeatureFlagValueTypes.String,
				DisabledVariant = "off",
				DefinitionJson = "{\"variants\":{\"on\":\"a\",\"off\":\"b\"},\"defaultVariant\":\"on\"}",
				Description = "A demo flag.",
				UpdatedAt = DateTime.UtcNow
			});
		});

		var handler = new GetFeatureFlagByKeyQueryHandler(context);

		var result = await handler.HandleAsync(
			new GetFeatureFlagByKeyQuery("demo-banner"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal("demo-banner", result.Value?.Key);
		Assert.Equal(FeatureFlagValueTypes.String, result.Value?.ValueType);
		Assert.Equal("off", result.Value?.DisabledVariant.GetValueOrNull());
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenKeyIsBlank()
	{
		var handler = new GetFeatureFlagByKeyQueryHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			new GetFeatureFlagByKeyQuery("   "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenFlagDoesNotExist()
	{
		var handler = new GetFeatureFlagByKeyQueryHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			new GetFeatureFlagByKeyQuery("missing-flag"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenTheContextIsDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		await context.DisposeAsync();

		var handler = new GetFeatureFlagByKeyQueryHandler(context);

		var result = await handler.HandleAsync(
			new GetFeatureFlagByKeyQuery("demo-banner"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
