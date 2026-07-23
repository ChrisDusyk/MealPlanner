using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.FeatureFlags.Queries;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.FeatureFlags.Queries;

public class GetFeatureFlagsTests
{
	private static void Seed(MealPlannerDbContext db, string key, bool enabled)
	{
		db.FeatureFlags.Add(new FeatureFlagEntity
		{
			Key = key,
			Enabled = enabled,
			DefinitionJson = "{\"defaultVariant\":\"on\"}",
			Description = null,
			UpdatedAt = DateTime.UtcNow
		});
	}

	[Fact]
	public async Task HandleAsync_ReturnsEmptyList_WhenNoFlagsExist()
	{
		var handler = new GetFeatureFlagsQueryHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(new GetFeatureFlagsQuery(), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Empty(result.Value!);
	}

	[Fact]
	public async Task HandleAsync_ReturnsFlagsOrderedByKey()
	{
		var db = TestDbContextFactory.CreateContext(context =>
		{
			Seed(context, "zeta", enabled: true);
			Seed(context, "alpha", enabled: false);
		});
		var handler = new GetFeatureFlagsQueryHandler(db);

		var result = await handler.HandleAsync(new GetFeatureFlagsQuery(), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Collection(result.Value!,
			flag => Assert.Equal("alpha", flag.Key),
			flag => Assert.Equal("zeta", flag.Key));
	}
}
