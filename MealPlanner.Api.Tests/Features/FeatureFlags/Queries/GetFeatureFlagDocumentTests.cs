using System.Text.Json;
using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.FeatureFlags.Queries;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.FeatureFlags.Queries;

public class GetFeatureFlagDocumentTests
{
	[Fact]
	public async Task HandleAsync_ReturnsFlagdDocument_ReflectingSeededFlags()
	{
		var db = TestDbContextFactory.CreateContext(context =>
		{
			context.FeatureFlags.Add(new FeatureFlagEntity
			{
				Key = "demo-banner",
				Enabled = true,
				DefinitionJson = "{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"on\"}",
				Description = null,
				UpdatedAt = DateTime.UtcNow
			});
		});
		var handler = new GetFeatureFlagDocumentQueryHandler(db);

		var result = await handler.HandleAsync(
			new GetFeatureFlagDocumentQuery(), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);

		using var document = JsonDocument.Parse(result.Value!);
		var flag = document.RootElement.GetProperty("flags").GetProperty("demo-banner");
		Assert.Equal("ENABLED", flag.GetProperty("state").GetString());
		Assert.Equal("on", flag.GetProperty("defaultVariant").GetString());
	}

	[Fact]
	public async Task HandleAsync_ReturnsDocumentWithEmptyFlags_WhenNoneExist()
	{
		var handler = new GetFeatureFlagDocumentQueryHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			new GetFeatureFlagDocumentQuery(), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);

		using var document = JsonDocument.Parse(result.Value!);
		Assert.Empty(document.RootElement.GetProperty("flags").EnumerateObject());
	}
}
