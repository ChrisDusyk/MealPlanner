using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.FeatureFlags.Models;

/// <summary>
/// Domain model for a feature flag definition. Immutable; the
/// <see cref="DefinitionJson"/> carries the flagd flag body (variants,
/// defaultVariant, optional targeting) as a JSON object string.
/// </summary>
public record FeatureFlag(
	string Key,
	bool Enabled,
	string DefinitionJson,
	Option<string> Description,
	DateTime UpdatedAt);
