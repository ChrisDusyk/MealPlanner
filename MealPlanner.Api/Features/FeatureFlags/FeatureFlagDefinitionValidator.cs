using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.FeatureFlags;

/// <summary>
/// Validates the parts of a feature flag an admin can author. The sync mapper
/// deliberately tolerates malformed rows so one bad definition cannot break the
/// whole flagd document; this validator is the counterpart that stops such a row
/// being written in the first place.
/// </summary>
public static partial class FeatureFlagDefinitionValidator
{
	/// <summary>
	/// Lowercase kebab-ish keys only. flagd keys travel in JSON documents and
	/// URLs, and a predictable shape keeps them greppable in calling code.
	/// </summary>
	[GeneratedRegex("^[a-z0-9][a-z0-9-]*$")]
	private static partial Regex KeyPattern();

	internal const int MaxKeyLength = 200;

	internal static Result<Unit> ValidateKey(string? key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return Failure("A feature flag key is required.");
		}

		if (key.Length > MaxKeyLength)
		{
			return Failure($"A feature flag key must be {MaxKeyLength} characters or fewer.");
		}

		return KeyPattern().IsMatch(key)
			? Success()
			: Failure("A feature flag key may only contain lowercase letters, numbers, and hyphens, and must start with a letter or number.");
	}

	/// <summary>
	/// Validates a flag body (<c>variants</c> / <c>defaultVariant</c> / optional
	/// <c>targeting</c>) against the declared value type, plus the disabled-state
	/// variant stored alongside it.
	/// </summary>
	internal static Result<Unit> ValidateDefinition(
		string valueType,
		string? disabledVariant,
		string definitionJson)
	{
		if (!FeatureFlagValueTypes.IsSupported(valueType))
		{
			return Failure(
				$"Value type must be one of: {string.Join(", ", FeatureFlagValueTypes.All)}.");
		}

		JsonObject body;
		try
		{
			body = JsonNode.Parse(definitionJson ?? string.Empty) as JsonObject
				?? throw new InvalidOperationException();
		}
		catch (Exception)
		{
			return Failure("The flag definition must be a JSON object.");
		}

		if (body["variants"] is not JsonObject variants || variants.Count == 0)
		{
			return Failure("The flag definition must declare at least one variant.");
		}

		foreach (var (name, value) in variants)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return Failure("Variant names cannot be blank.");
			}

			if (!MatchesValueType(value, valueType))
			{
				return Failure($"Variant '{name}' is not a valid {valueType} value.");
			}
		}

		if (body["defaultVariant"] is not JsonValue defaultNode
			|| !defaultNode.TryGetValue<string>(out var defaultVariant)
			|| string.IsNullOrWhiteSpace(defaultVariant))
		{
			return Failure("A default variant is required.");
		}

		if (!variants.ContainsKey(defaultVariant))
		{
			return Failure($"The default variant '{defaultVariant}' is not one of the flag's variants.");
		}

		if (!string.IsNullOrWhiteSpace(disabledVariant) && !variants.ContainsKey(disabledVariant))
		{
			return Failure($"The disabled variant '{disabledVariant}' is not one of the flag's variants.");
		}

		return ValidateTargeting(body["targeting"], variants);
	}

	/// <summary>
	/// Checks the optional targeting block. The rules themselves are JsonLogic
	/// evaluated inside flagd, so this does not attempt to interpret them — it
	/// confirms the block is an object and that any variant a rule can select
	/// actually exists, which is the failure mode that silently serves the wrong
	/// value in production.
	/// </summary>
	private static Result<Unit> ValidateTargeting(JsonNode? targeting, JsonObject variants)
	{
		if (targeting is null)
		{
			return Success();
		}

		if (targeting is not JsonObject targetingObject)
		{
			return Failure("Targeting rules must be a JSON object.");
		}

		foreach (var variantName in CollectReferencedVariants(targetingObject))
		{
			if (!variants.ContainsKey(variantName))
			{
				return Failure($"Targeting references variant '{variantName}', which is not defined.");
			}
		}

		return Success();
	}

	/// <summary>
	/// Walks the targeting tree for the two places a variant name appears as a
	/// literal: the buckets of a <c>fractional</c> operator, and the branches of
	/// an <c>if</c>. Other string literals are operands (attribute names, match
	/// values) and must not be treated as variants.
	/// </summary>
	private static IEnumerable<string> CollectReferencedVariants(JsonNode? node)
	{
		switch (node)
		{
			case JsonObject obj:
				foreach (var (key, value) in obj)
				{
					if (key == "fractional" && value is JsonArray buckets)
					{
						// Each bucket is [variantName, weight]; the first entry may
						// instead be the bucketing expression, which is not an array.
						foreach (var bucket in buckets.OfType<JsonArray>())
						{
							if (bucket.Count > 0 && bucket[0] is JsonValue name
								&& name.TryGetValue<string>(out var variantName))
							{
								yield return variantName;
							}
						}

						continue;
					}

					if (key == "if" && value is JsonArray branches)
					{
						// [condition, then, condition, then, ..., else] — every
						// odd index plus a trailing else slot is a returned value.
						for (var i = 1; i < branches.Count; i += 2)
						{
							if (branches[i] is JsonValue thenValue
								&& thenValue.TryGetValue<string>(out var thenVariant))
							{
								yield return thenVariant;
							}
						}

						if (branches.Count > 2 && branches.Count % 2 == 1
							&& branches[^1] is JsonValue elseValue
							&& elseValue.TryGetValue<string>(out var elseVariant))
						{
							yield return elseVariant;
						}
					}

					foreach (var nested in CollectReferencedVariants(value))
					{
						yield return nested;
					}
				}

				break;

			case JsonArray array:
				foreach (var item in array)
				{
					foreach (var nested in CollectReferencedVariants(item))
					{
						yield return nested;
					}
				}

				break;
		}
	}

	private static bool MatchesValueType(JsonNode? value, string valueType) => valueType switch
	{
		FeatureFlagValueTypes.Boolean => value is JsonValue b && b.TryGetValue<bool>(out _),
		FeatureFlagValueTypes.String => value is JsonValue s && s.TryGetValue<string>(out _),
		FeatureFlagValueTypes.Number => value is JsonValue n && n.TryGetValue<double>(out _),
		FeatureFlagValueTypes.Object => value is JsonObject or JsonArray,
		_ => false
	};

	private static Result<Unit> Success() => Result<Unit>.Success(Unit.Value);

	private static Result<Unit> Failure(string message) =>
		Result<Unit>.Failure(new Error(ErrorCodes.ValidationFailed, message));
}
