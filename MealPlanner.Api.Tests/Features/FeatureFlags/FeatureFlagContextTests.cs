using MealPlanner.Api.Features.FeatureFlags;

namespace MealPlanner.Api.Tests.Features.FeatureFlags;

public class FeatureFlagContextTests
{
	[Fact]
	public void From_WrapsSuppliedValues()
	{
		var context = FeatureFlagContext.From("user-1", "chef@example.com", "admin");

		Assert.Equal("user-1", context.TargetingKey.GetValueOrNull());
		Assert.Equal("chef@example.com", context.Email.GetValueOrNull());
		Assert.Equal("admin", context.Role.GetValueOrNull());
	}

	[Fact]
	public void From_MapsNullsToNone()
	{
		var context = FeatureFlagContext.From(null);

		Assert.False(context.TargetingKey.HasValue);
		Assert.False(context.Email.HasValue);
		Assert.False(context.Role.HasValue);
	}

	[Fact]
	public void ToEvaluationContext_ReturnsNull_WhenNothingIsSet()
	{
		Assert.Null(FeatureFlagContext.Empty.ToEvaluationContext());
	}

	[Fact]
	public void ToEvaluationContext_SetsTheTargetingKeyAndAttributes()
	{
		var evaluationContext = FeatureFlagContext
			.From("user-1", "chef@example.com", "admin")
			.ToEvaluationContext();

		Assert.NotNull(evaluationContext);
		Assert.Equal("user-1", evaluationContext.TargetingKey);
		Assert.True(evaluationContext.TryGetValue("email", out var email));
		Assert.Equal("chef@example.com", email?.AsString);
		Assert.True(evaluationContext.TryGetValue("role", out var role));
		Assert.Equal("admin", role?.AsString);
	}

	[Fact]
	public void ToEvaluationContext_OmitsAbsentAttributes()
	{
		var evaluationContext = FeatureFlagContext.From("user-1").ToEvaluationContext();

		Assert.NotNull(evaluationContext);
		Assert.Equal("user-1", evaluationContext.TargetingKey);
		Assert.False(evaluationContext.TryGetValue("email", out _));
		Assert.False(evaluationContext.TryGetValue("role", out _));
	}
}
