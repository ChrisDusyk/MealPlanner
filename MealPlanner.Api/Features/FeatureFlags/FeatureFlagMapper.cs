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
		DefinitionJson: entity.DefinitionJson,
		Description: Option<string>.From(entity.Description),
		UpdatedAt: entity.UpdatedAt);

	/// <summary>
	/// Assembles the supplied flags into a flagd-format document. Each flag's
	/// <see cref="FeatureFlag.DefinitionJson"/> supplies the variants /
	/// defaultVariant / targeting, and the current <see cref="FeatureFlag.Enabled"/>
	/// state is merged in as the flagd <c>state</c> field.
	/// </summary>
	internal static string ToFlagdDocument(IEnumerable<FeatureFlag> flags)
	{
		var flagsObject = new JsonObject();

		foreach (var flag in flags)
		{
			var body = ParseDefinition(flag.DefinitionJson);
			body["state"] = flag.Enabled ? "ENABLED" : "DISABLED";
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
