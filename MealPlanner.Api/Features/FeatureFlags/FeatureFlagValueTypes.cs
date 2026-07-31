namespace MealPlanner.Api.Features.FeatureFlags;

/// <summary>
/// The JSON kinds a feature flag's variants may take. flagd requires every
/// variant of a flag to share one kind, so a flag carries exactly one of these.
/// </summary>
public static class FeatureFlagValueTypes
{
	public const string Boolean = "boolean";
	public const string String = "string";
	public const string Number = "number";
	public const string Object = "object";

	public static readonly IReadOnlyList<string> All = [Boolean, String, Number, Object];

	public static bool IsSupported(string? valueType) =>
		valueType is not null && All.Contains(valueType);
}
