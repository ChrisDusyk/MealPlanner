using System.Text.Json;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.FeatureFlags;
using MealPlanner.Api.Features.FeatureFlags.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.FeatureFlags;

public class FeatureFlagMapperTests
{
	private static FeatureFlag Flag(
		string key,
		bool enabled,
		string definitionJson,
		string? description = null,
		string? disabledVariant = null,
		string valueType = FeatureFlagValueTypes.Boolean) =>
		new(
			key,
			enabled,
			valueType,
			Option<string>.From(disabledVariant),
			definitionJson,
			Option<string>.From(description),
			DateTime.UtcNow);

	[Fact]
	public void ToDomain_MapsAllFields_AndWrapsDescriptionInSome()
	{
		var updatedAt = new DateTime(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);
		var entity = new FeatureFlagEntity
		{
			Key = "demo-banner",
			Enabled = true,
			ValueType = FeatureFlagValueTypes.String,
			DisabledVariant = "off",
			DefinitionJson = "{\"defaultVariant\":\"on\"}",
			Description = "A demo flag.",
			UpdatedAt = updatedAt
		};

		var domain = FeatureFlagMapper.ToDomain(entity);

		Assert.Equal("demo-banner", domain.Key);
		Assert.True(domain.Enabled);
		Assert.Equal(FeatureFlagValueTypes.String, domain.ValueType);
		Assert.True(domain.DisabledVariant.HasValue);
		Assert.Equal("off", domain.DisabledVariant.Value);
		Assert.Equal("{\"defaultVariant\":\"on\"}", domain.DefinitionJson);
		Assert.True(domain.Description.HasValue);
		Assert.Equal("A demo flag.", domain.Description.Value);
		Assert.Equal(updatedAt, domain.UpdatedAt);
	}

	[Fact]
	public void ToDomain_MapsNullDescription_ToNone()
	{
		var entity = new FeatureFlagEntity
		{
			Key = "demo-banner",
			Enabled = false,
			DefinitionJson = "{}",
			Description = null,
			UpdatedAt = DateTime.UtcNow
		};

		var domain = FeatureFlagMapper.ToDomain(entity);

		Assert.False(domain.Description.HasValue);
		Assert.False(domain.DisabledVariant.HasValue);
	}

	[Fact]
	public void ToFlagdDocument_IncludesSchema_AndFlagsObject()
	{
		var json = FeatureFlagMapper.ToFlagdDocument([]);

		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;

		Assert.Equal(FeatureFlagMapper.FlagdSchema, root.GetProperty("$schema").GetString());
		Assert.Equal(JsonValueKind.Object, root.GetProperty("flags").ValueKind);
		Assert.Empty(root.GetProperty("flags").EnumerateObject());
	}

	[Fact]
	public void ToFlagdDocument_SetsEnabledState_AndPreservesDefinitionBody()
	{
		var flags = new[]
		{
			Flag("demo-banner", enabled: true, "{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"on\"}")
		};

		var json = FeatureFlagMapper.ToFlagdDocument(flags);

		using var document = JsonDocument.Parse(json);
		var flag = document.RootElement.GetProperty("flags").GetProperty("demo-banner");

		Assert.Equal("ENABLED", flag.GetProperty("state").GetString());
		Assert.Equal("on", flag.GetProperty("defaultVariant").GetString());
		Assert.True(flag.GetProperty("variants").GetProperty("on").GetBoolean());
		Assert.False(flag.GetProperty("variants").GetProperty("off").GetBoolean());
	}

	[Fact]
	public void ToFlagdDocument_SetsDisabledState_WhenFlagIsDisabledWithoutADisabledVariant()
	{
		var flags = new[] { Flag("demo-banner", enabled: false, "{\"defaultVariant\":\"on\"}") };

		var json = FeatureFlagMapper.ToFlagdDocument(flags);

		using var document = JsonDocument.Parse(json);
		var flag = document.RootElement.GetProperty("flags").GetProperty("demo-banner");

		Assert.Equal("DISABLED", flag.GetProperty("state").GetString());
	}

	[Fact]
	public void ToFlagdDocument_ServesTheDisabledVariant_WhenFlagIsDisabled()
	{
		// flagd resolves a DISABLED flag to the caller's code default, so a flag
		// that names a disabled variant stays ENABLED with its default swapped.
		var flags = new[]
		{
			Flag(
				"demo-banner",
				enabled: false,
				"{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"on\"}",
				disabledVariant: "off")
		};

		var json = FeatureFlagMapper.ToFlagdDocument(flags);

		using var document = JsonDocument.Parse(json);
		var flag = document.RootElement.GetProperty("flags").GetProperty("demo-banner");

		Assert.Equal("ENABLED", flag.GetProperty("state").GetString());
		Assert.Equal("off", flag.GetProperty("defaultVariant").GetString());
	}

	[Fact]
	public void ToFlagdDocument_DropsTargeting_WhenFlagIsDisabledWithADisabledVariant()
	{
		// Switching a flag off must be a kill switch, not something a targeting
		// rule can override.
		var flags = new[]
		{
			Flag(
				"demo-banner",
				enabled: false,
				"{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"on\"," +
				"\"targeting\":{\"if\":[{\"==\":[{\"var\":\"role\"},\"admin\"]},\"on\"]}}",
				disabledVariant: "off")
		};

		var json = FeatureFlagMapper.ToFlagdDocument(flags);

		using var document = JsonDocument.Parse(json);
		var flag = document.RootElement.GetProperty("flags").GetProperty("demo-banner");

		Assert.False(flag.TryGetProperty("targeting", out _));
	}

	[Fact]
	public void ToFlagdDocument_KeepsTargeting_WhenFlagIsEnabled()
	{
		var flags = new[]
		{
			Flag(
				"demo-banner",
				enabled: true,
				"{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"off\"," +
				"\"targeting\":{\"if\":[{\"==\":[{\"var\":\"role\"},\"admin\"]},\"on\"]}}",
				disabledVariant: "off")
		};

		var json = FeatureFlagMapper.ToFlagdDocument(flags);

		using var document = JsonDocument.Parse(json);
		var flag = document.RootElement.GetProperty("flags").GetProperty("demo-banner");

		Assert.Equal("ENABLED", flag.GetProperty("state").GetString());
		Assert.Equal("off", flag.GetProperty("defaultVariant").GetString());
		Assert.True(flag.TryGetProperty("targeting", out _));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("not-json")]
	[InlineData("[1,2,3]")]
	public void ToFlagdDocument_FallsBackToStateOnly_WhenDefinitionIsMissingOrInvalid(string definition)
	{
		var flags = new[] { Flag("broken", enabled: true, definition) };

		var json = FeatureFlagMapper.ToFlagdDocument(flags);

		using var document = JsonDocument.Parse(json);
		var flag = document.RootElement.GetProperty("flags").GetProperty("broken");

		// A malformed definition must not break the whole document; the flag still
		// appears with its state so flagd can serve it.
		Assert.Equal("ENABLED", flag.GetProperty("state").GetString());
	}

	[Fact]
	public void ToFlagdDocument_EmitsEveryFlag()
	{
		var flags = new[]
		{
			Flag("a", enabled: true, "{\"defaultVariant\":\"on\"}"),
			Flag("b", enabled: false, "{\"defaultVariant\":\"on\"}")
		};

		var json = FeatureFlagMapper.ToFlagdDocument(flags);

		using var document = JsonDocument.Parse(json);
		var flagsElement = document.RootElement.GetProperty("flags");

		Assert.Equal(2, flagsElement.EnumerateObject().Count());
		Assert.Equal("ENABLED", flagsElement.GetProperty("a").GetProperty("state").GetString());
		Assert.Equal("DISABLED", flagsElement.GetProperty("b").GetProperty("state").GetString());
	}
}
