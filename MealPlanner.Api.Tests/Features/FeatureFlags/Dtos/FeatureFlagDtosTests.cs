using MealPlanner.Api.Features.FeatureFlags;
using MealPlanner.Api.Features.FeatureFlags.Dtos;
using MealPlanner.Api.Features.FeatureFlags.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.FeatureFlags.Dtos;

public class FeatureFlagDtosTests
{
	[Fact]
	public void FromDomain_MapsEveryField()
	{
		var updatedAt = new DateTime(2026, 7, 31, 9, 0, 0, DateTimeKind.Utc);
		var flag = new FeatureFlag(
			Key: "demo-banner",
			Enabled: true,
			ValueType: FeatureFlagValueTypes.Number,
			DisabledVariant: Option<string>.Some("off"),
			DefinitionJson: "{\"variants\":{\"on\":1,\"off\":0},\"defaultVariant\":\"on\"}",
			Description: Option<string>.Some("A demo flag."),
			UpdatedAt: updatedAt);

		var dto = FeatureFlagDto.FromDomain(flag);

		Assert.Equal("demo-banner", dto.Key);
		Assert.True(dto.Enabled);
		Assert.Equal(FeatureFlagValueTypes.Number, dto.ValueType);
		Assert.Equal("off", dto.DisabledVariant);
		Assert.Equal("{\"variants\":{\"on\":1,\"off\":0},\"defaultVariant\":\"on\"}", dto.DefinitionJson);
		Assert.Equal("A demo flag.", dto.Description);
		Assert.Equal(updatedAt, dto.UpdatedAt);
	}

	[Fact]
	public void FromDomain_MapsNoneOptions_ToNull()
	{
		var flag = new FeatureFlag(
			Key: "demo-banner",
			Enabled: false,
			ValueType: FeatureFlagValueTypes.Boolean,
			DisabledVariant: Option<string>.None(),
			DefinitionJson: "{}",
			Description: Option<string>.None(),
			UpdatedAt: DateTime.UtcNow);

		var dto = FeatureFlagDto.FromDomain(flag);

		Assert.Null(dto.DisabledVariant);
		Assert.Null(dto.Description);
	}
}
