using MealPlanner.Api.Features.FeatureFlags;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.FeatureFlags;

public class FeatureFlagDefinitionValidatorTests
{
	private const string BooleanDefinition =
		"{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"on\"}";

	[Theory]
	[InlineData("demo-banner")]
	[InlineData("a")]
	[InlineData("2026-rollout")]
	public void ValidateKey_Succeeds_ForLowercaseKebabKeys(string key)
	{
		Assert.True(FeatureFlagDefinitionValidator.ValidateKey(key).IsSuccess);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("Demo-Banner")]
	[InlineData("demo banner")]
	[InlineData("demo_banner")]
	[InlineData("-leading-hyphen")]
	public void ValidateKey_Fails_ForInvalidKeys(string? key)
	{
		var result = FeatureFlagDefinitionValidator.ValidateKey(key);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public void ValidateKey_Fails_WhenKeyExceedsMaxLength()
	{
		var key = new string('a', FeatureFlagDefinitionValidator.MaxKeyLength + 1);

		var result = FeatureFlagDefinitionValidator.ValidateKey(key);

		Assert.False(result.IsSuccess);
		Assert.Contains("characters or fewer", result.Error?.Message);
	}

	[Fact]
	public void ValidateDefinition_Succeeds_ForAWellFormedBooleanFlag()
	{
		var result = FeatureFlagDefinitionValidator.ValidateDefinition(
			FeatureFlagValueTypes.Boolean, "off", BooleanDefinition);

		Assert.True(result.IsSuccess);
	}

	[Fact]
	public void ValidateDefinition_Succeeds_WhenNoDisabledVariantIsSet()
	{
		var result = FeatureFlagDefinitionValidator.ValidateDefinition(
			FeatureFlagValueTypes.Boolean, null, BooleanDefinition);

		Assert.True(result.IsSuccess);
	}

	[Theory]
	[InlineData("bool")]
	[InlineData("")]
	[InlineData(null)]
	public void ValidateDefinition_Fails_ForAnUnsupportedValueType(string? valueType)
	{
		var result = FeatureFlagDefinitionValidator.ValidateDefinition(
			valueType!, null, BooleanDefinition);

		Assert.False(result.IsSuccess);
		Assert.Contains("Value type", result.Error?.Message);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("not-json")]
	[InlineData("[1,2,3]")]
	[InlineData("\"a string\"")]
	public void ValidateDefinition_Fails_WhenTheBodyIsNotAJsonObject(string definition)
	{
		var result = FeatureFlagDefinitionValidator.ValidateDefinition(
			FeatureFlagValueTypes.Boolean, null, definition);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Theory]
	[InlineData("{\"defaultVariant\":\"on\"}")]
	[InlineData("{\"variants\":{},\"defaultVariant\":\"on\"}")]
	public void ValidateDefinition_Fails_WhenThereAreNoVariants(string definition)
	{
		var result = FeatureFlagDefinitionValidator.ValidateDefinition(
			FeatureFlagValueTypes.Boolean, null, definition);

		Assert.False(result.IsSuccess);
		Assert.Contains("at least one variant", result.Error?.Message);
	}

	[Fact]
	public void ValidateDefinition_Fails_WhenAVariantDoesNotMatchTheValueType()
	{
		var result = FeatureFlagDefinitionValidator.ValidateDefinition(
			FeatureFlagValueTypes.Boolean,
			null,
			"{\"variants\":{\"on\":true,\"off\":\"nope\"},\"defaultVariant\":\"on\"}");

		Assert.False(result.IsSuccess);
		Assert.Contains("'off'", result.Error?.Message);
	}

	[Theory]
	[InlineData(FeatureFlagValueTypes.String, "{\"variants\":{\"a\":\"x\",\"b\":\"y\"},\"defaultVariant\":\"a\"}")]
	[InlineData(FeatureFlagValueTypes.Number, "{\"variants\":{\"low\":1,\"high\":2.5},\"defaultVariant\":\"low\"}")]
	[InlineData(FeatureFlagValueTypes.Object, "{\"variants\":{\"a\":{\"x\":1}},\"defaultVariant\":\"a\"}")]
	public void ValidateDefinition_Succeeds_ForNonBooleanValueTypes(string valueType, string definition)
	{
		Assert.True(
			FeatureFlagDefinitionValidator.ValidateDefinition(valueType, null, definition).IsSuccess);
	}

	[Theory]
	[InlineData("{\"variants\":{\"on\":true},\"defaultVariant\":\"missing\"}")]
	[InlineData("{\"variants\":{\"on\":true}}")]
	[InlineData("{\"variants\":{\"on\":true},\"defaultVariant\":42}")]
	public void ValidateDefinition_Fails_WhenTheDefaultVariantIsMissingOrUnknown(string definition)
	{
		var result = FeatureFlagDefinitionValidator.ValidateDefinition(
			FeatureFlagValueTypes.Boolean, null, definition);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public void ValidateDefinition_Fails_WhenTheDisabledVariantIsUnknown()
	{
		var result = FeatureFlagDefinitionValidator.ValidateDefinition(
			FeatureFlagValueTypes.Boolean, "nope", BooleanDefinition);

		Assert.False(result.IsSuccess);
		Assert.Contains("disabled variant", result.Error?.Message);
	}

	[Fact]
	public void ValidateDefinition_Fails_WhenTargetingIsNotAnObject()
	{
		var result = FeatureFlagDefinitionValidator.ValidateDefinition(
			FeatureFlagValueTypes.Boolean,
			null,
			"{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"on\",\"targeting\":[1,2]}");

		Assert.False(result.IsSuccess);
		Assert.Contains("Targeting rules", result.Error?.Message);
	}

	[Fact]
	public void ValidateDefinition_Succeeds_ForAnIfRuleReferencingKnownVariants()
	{
		var result = FeatureFlagDefinitionValidator.ValidateDefinition(
			FeatureFlagValueTypes.Boolean,
			null,
			"{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"off\"," +
			"\"targeting\":{\"if\":[{\"==\":[{\"var\":\"role\"},\"admin\"]},\"on\",\"off\"]}}");

		Assert.True(result.IsSuccess);
	}

	[Fact]
	public void ValidateDefinition_Fails_WhenAnIfRuleReferencesAnUnknownVariant()
	{
		var result = FeatureFlagDefinitionValidator.ValidateDefinition(
			FeatureFlagValueTypes.Boolean,
			null,
			"{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"off\"," +
			"\"targeting\":{\"if\":[{\"==\":[{\"var\":\"role\"},\"admin\"]},\"beta\"]}}");

		Assert.False(result.IsSuccess);
		Assert.Contains("'beta'", result.Error?.Message);
	}

	[Fact]
	public void ValidateDefinition_Fails_WhenAFractionalBucketReferencesAnUnknownVariant()
	{
		var result = FeatureFlagDefinitionValidator.ValidateDefinition(
			FeatureFlagValueTypes.Boolean,
			null,
			"{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"off\"," +
			"\"targeting\":{\"fractional\":[[\"on\",50],[\"ghost\",50]]}}");

		Assert.False(result.IsSuccess);
		Assert.Contains("'ghost'", result.Error?.Message);
	}

	[Theory]
	// The bucketing expression sits alongside the buckets and may be written in
	// either JsonLogic form. Neither should be mistaken for a bucket.
	[InlineData("{\"var\":\"targetingKey\"}")]
	[InlineData("[\"var\",\"targetingKey\"]")]
	public void ValidateDefinition_IgnoresTheBucketingExpression_WhateverFormItTakes(string bucketBy)
	{
		var result = FeatureFlagDefinitionValidator.ValidateDefinition(
			FeatureFlagValueTypes.Boolean,
			null,
			"{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"off\"," +
			$"\"targeting\":{{\"fractional\":[{bucketBy},[\"on\",25],[\"off\",75]]}}}}");

		Assert.True(result.IsSuccess);
	}

	[Fact]
	public void ValidateDefinition_Succeeds_ForAFractionalRolloutOverKnownVariants()
	{
		var result = FeatureFlagDefinitionValidator.ValidateDefinition(
			FeatureFlagValueTypes.Boolean,
			null,
			"{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"off\"," +
			"\"targeting\":{\"fractional\":[{\"var\":\"targetingKey\"},[\"on\",25],[\"off\",75]]}}");

		Assert.True(result.IsSuccess);
	}

	[Fact]
	public void ValidateDefinition_IgnoresOperandStrings_WhenCollectingVariantReferences()
	{
		// "role" and "admin" are operands, not variant names — treating them as
		// variants would reject every realistic rule.
		var result = FeatureFlagDefinitionValidator.ValidateDefinition(
			FeatureFlagValueTypes.Boolean,
			null,
			"{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"off\"," +
			"\"targeting\":{\"if\":[{\"starts_with\":[{\"var\":\"email\"},\"admin@\"]},\"on\"]}}");

		Assert.True(result.IsSuccess);
	}
}
