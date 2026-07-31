using System.ComponentModel.DataAnnotations;
using MealPlanner.Api.Features.FeatureFlags.Models;

namespace MealPlanner.Api.Features.FeatureFlags.Dtos;

/// <summary>
/// Admin-facing representation of a feature flag, returned by the admin
/// endpoints and consumed by the frontend admin screens.
/// </summary>
public class FeatureFlagDto
{
	public string Key { get; set; } = string.Empty;
	public bool Enabled { get; set; }
	public string ValueType { get; set; } = FeatureFlagValueTypes.Boolean;
	public string? DisabledVariant { get; set; }
	public string DefinitionJson { get; set; } = string.Empty;
	public string? Description { get; set; }
	public DateTime UpdatedAt { get; set; }

	public static FeatureFlagDto FromDomain(FeatureFlag flag) => new()
	{
		Key = flag.Key,
		Enabled = flag.Enabled,
		ValueType = flag.ValueType,
		DisabledVariant = flag.DisabledVariant.GetValueOrNull(),
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

/// <summary>
/// Request body for creating a flag. Only shape is enforced here; the semantic
/// checks (variant value types, default and disabled variants resolving to real
/// variants, targeting shape) live in <see cref="FeatureFlagDefinitionValidator"/>
/// and run inside the handler.
/// </summary>
public class CreateFeatureFlagRequest
{
	[Required]
	[MaxLength(FeatureFlagDefinitionValidator.MaxKeyLength)]
	public string Key { get; set; } = string.Empty;

	[Required]
	[MaxLength(32)]
	public string ValueType { get; set; } = FeatureFlagValueTypes.Boolean;

	[Required]
	public string DefinitionJson { get; set; } = string.Empty;

	[MaxLength(200)]
	public string? DisabledVariant { get; set; }

	public string? Description { get; set; }

	public bool Enabled { get; set; }
}

/// <summary>
/// Request body for updating a flag. The key comes from the route and cannot be
/// changed — calling code references flags by key.
/// </summary>
public class UpdateFeatureFlagRequest
{
	[Required]
	[MaxLength(32)]
	public string ValueType { get; set; } = FeatureFlagValueTypes.Boolean;

	[Required]
	public string DefinitionJson { get; set; } = string.Empty;

	[MaxLength(200)]
	public string? DisabledVariant { get; set; }

	public string? Description { get; set; }

	public bool Enabled { get; set; }
}

/// <summary>
/// Request body for the admin dry-run evaluation. Every field is optional; the
/// supplied values become the OpenFeature evaluation context.
/// </summary>
public class EvaluateFeatureFlagRequest
{
	public string? TargetingKey { get; set; }
	public string? Email { get; set; }
	public string? Role { get; set; }
}

/// <summary>
/// Result of a dry-run evaluation. Reflects the document flagd last synced, so
/// unsaved edits are not visible here.
/// </summary>
public class EvaluateFeatureFlagResponse
{
	public string Key { get; set; } = string.Empty;
	public string ValueType { get; set; } = FeatureFlagValueTypes.Boolean;

	/// <summary>
	/// The resolved value rendered as JSON, so booleans, strings, numbers, and
	/// objects can all travel through one field.
	/// </summary>
	public string ValueJson { get; set; } = string.Empty;
}
