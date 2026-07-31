using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.FeatureFlags.Models;

/// <summary>
/// Domain model for a feature flag definition. Immutable; the
/// <see cref="DefinitionJson"/> carries the flagd flag body (variants,
/// defaultVariant, optional targeting) as a JSON object string.
/// </summary>
/// <param name="ValueType">
/// JSON kind shared by every variant — see <see cref="FeatureFlagValueTypes"/>.
/// </param>
/// <param name="DisabledVariant">
/// Variant served while the flag is off. <c>None</c> falls back to emitting
/// <c>state: DISABLED</c>, which resolves to the caller's code default.
/// </param>
public record FeatureFlag(
	string Key,
	bool Enabled,
	string ValueType,
	Option<string> DisabledVariant,
	string DefinitionJson,
	Option<string> Description,
	DateTime UpdatedAt);
