using MealPlanner.Api.Features.FeatureFlags.Models;

namespace MealPlanner.Api.Features.FeatureFlags.Dtos;

/// <summary>
/// Admin-facing representation of a feature flag, returned by the admin list
/// endpoint and consumed by the frontend admin toggle screen.
/// </summary>
public class FeatureFlagDto
{
	public string Key { get; set; } = string.Empty;
	public bool Enabled { get; set; }
	public string DefinitionJson { get; set; } = string.Empty;
	public string? Description { get; set; }
	public DateTime UpdatedAt { get; set; }

	public static FeatureFlagDto FromDomain(FeatureFlag flag) => new()
	{
		Key = flag.Key,
		Enabled = flag.Enabled,
		DefinitionJson = flag.DefinitionJson,
		Description = flag.Description.GetValueOrNull(),
		UpdatedAt = flag.UpdatedAt
	};
}

/// <summary>
/// Request body for toggling a flag's enabled state.
/// </summary>
public class SetFeatureFlagEnabledRequest
{
	public bool Enabled { get; set; }
}
