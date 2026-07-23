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
	/// Whether the flag is active. Maps to the flagd <c>state</c>
	/// (<c>ENABLED</c>/<c>DISABLED</c>). For a boolean flag whose
	/// <c>defaultVariant</c> points at its "true" variant, this doubles as the
	/// resolved value: enabled → true, disabled → the caller's code default.
	/// </summary>
	public bool Enabled { get; set; }

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
