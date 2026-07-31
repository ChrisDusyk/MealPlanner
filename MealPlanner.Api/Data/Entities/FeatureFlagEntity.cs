using MealPlanner.Api.Features.FeatureFlags;

namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for a feature flag definition. Rows in this table are the
/// mutable source of truth for flag configuration: the API assembles them into
/// a flagd-format document that flagd HTTP-syncs and hot-reloads, so toggling a
/// flag never requires a redeploy.
/// </summary>
public class FeatureFlagEntity
{
	/// <summary>
	/// The flag key used when evaluating the flag through OpenFeature
	/// (for example <c>demo-banner</c>). Serves as the primary key.
	/// </summary>
	public string Key { get; set; } = string.Empty;

	/// <summary>
	/// Whether the flag is active. When <see cref="DisabledVariant"/> is set this
	/// selects which variant the sync document serves as <c>defaultVariant</c>
	/// (see <see cref="FeatureFlagMapper"/>).
	/// When it is not set, this maps straight to the flagd <c>state</c>
	/// (<c>ENABLED</c>/<c>DISABLED</c>) and a disabled flag resolves to the
	/// caller's code default.
	/// </summary>
	public bool Enabled { get; set; }

	/// <summary>
	/// JSON kind shared by every variant value: <c>boolean</c>, <c>string</c>,
	/// <c>number</c>, or <c>object</c>. flagd requires a flag's variants to be
	/// homogeneous, so this drives both write validation and the admin editor's
	/// value inputs.
	/// </summary>
	public string ValueType { get; set; } = FeatureFlagValueTypes.Boolean;

	/// <summary>
	/// Variant served while the flag is switched off. When set, the sync document
	/// keeps the flag <c>ENABLED</c> and swaps <c>defaultVariant</c> to this
	/// variant (dropping any targeting), so the database — not the calling code's
	/// fallback — decides the value in both states. Null preserves the original
	/// behaviour of emitting <c>state: DISABLED</c>.
	/// </summary>
	public string? DisabledVariant { get; set; }

	/// <summary>
	/// The flagd flag body as a JSON object string, excluding <c>state</c>
	/// (for example <c>{"variants":{"on":true,"off":false},"defaultVariant":"on"}</c>).
	/// The sync mapper merges the current <see cref="Enabled"/> state into this
	/// object when building the flagd document.
	/// </summary>
	public string DefinitionJson { get; set; } = string.Empty;

	/// <summary>
	/// Optional human-readable description shown in the admin UI.
	/// </summary>
	public string? Description { get; set; }

	public DateTime UpdatedAt { get; set; }
}
