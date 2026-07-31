using System.Text.Json;
using System.Text.Json.Nodes;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.FeatureFlags.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.FeatureFlags;

/// <summary>
/// Maps between <see cref="FeatureFlagEntity"/> persistence rows, the
/// <see cref="FeatureFlag"/> domain model, and the flagd flag-configuration
/// document that flagd HTTP-syncs from the API.
/// </summary>
public static class FeatureFlagMapper
{
	internal const string FlagdSchema = "https://flagd.dev/schema/v0/flags.json";

	internal static FeatureFlag ToDomain(FeatureFlagEntity entity) => new(
		Key: entity.Key,
		Enabled: entity.Enabled,
		ValueType: entity.ValueType,
		DisabledVariant: Option<string>.From(entity.DisabledVariant),
		DefinitionJson: entity.DefinitionJson,
		Description: Option<string>.From(entity.Description),
		UpdatedAt: entity.UpdatedAt);

	/// <summary>
	/// Assembles the supplied flags into a flagd-format document. Each flag's
	/// <see cref="FeatureFlag.DefinitionJson"/> supplies the variants /
	/// defaultVariant / targeting, and the current <see cref="FeatureFlag.Enabled"/>
	/// state decides what the document serves — see <see cref="ApplyState"/>.
	/// </summary>
	internal static string ToFlagdDocument(IEnumerable<FeatureFlag> flags)
	{
		var flagsObject = new JsonObject();

		foreach (var flag in flags)
		{
			var body = ParseDefinition(flag.DefinitionJson);
			ApplyState(body, flag);
			flagsObject[flag.Key] = body;
		}

		var document = new JsonObject
		{
			["$schema"] = FlagdSchema,
			["flags"] = flagsObject
		};

		return document.ToJsonString();
	}

	/// <summary>
	/// Writes the flag's on/off state into its flagd body.
	/// <para>
	/// flagd has no notion of a stored "value when off": a flag whose state is
	/// <c>DISABLED</c> resolves to whatever default the calling code passed. So
	/// when a flag names a <see cref="FeatureFlag.DisabledVariant"/> the document
	/// keeps it <c>ENABLED</c> and instead points <c>defaultVariant</c> at that
	/// variant, which puts the resolved value under database control in both
	/// states. Targeting is dropped in that case so switching a flag off is a
	/// true kill switch rather than something rules can override.
	/// </para>
	/// <para>
	/// Flags with no disabled variant keep the original behaviour of emitting
	/// <c>state: DISABLED</c>, so pre-existing rows are unaffected.
	/// </para>
	/// </summary>
	private static void ApplyState(JsonObject body, FeatureFlag flag)
	{
		if (flag.Enabled)
		{
			body["state"] = "ENABLED";
			return;
		}

		if (!flag.DisabledVariant.HasValue)
		{
			body["state"] = "DISABLED";
			return;
		}

		body["state"] = "ENABLED";
		body["defaultVariant"] = flag.DisabledVariant.Value;
		body.Remove("targeting");
	}

	/// <summary>
	/// Parses a stored flag body into a mutable <see cref="JsonObject"/>. Falls
	/// back to an empty object when the stored definition is missing or not a
	/// JSON object, so a malformed row never breaks the whole sync document.
	/// </summary>
	private static JsonObject ParseDefinition(string definitionJson)
	{
		if (string.IsNullOrWhiteSpace(definitionJson))
		{
			return new JsonObject();
		}

		try
		{
			return JsonNode.Parse(definitionJson) as JsonObject ?? new JsonObject();
		}
		catch (JsonException)
		{
			return new JsonObject();
		}
	}
}
