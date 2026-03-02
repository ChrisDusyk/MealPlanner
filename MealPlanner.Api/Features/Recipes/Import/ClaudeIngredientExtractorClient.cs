using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using Microsoft.Extensions.Options;
using AppError = MealPlanner.Api.Shared.Error;

namespace MealPlanner.Api.Features.Recipes.Import;

public sealed class ClaudeIngredientExtractorClient(
	IHttpClientFactory httpClientFactory,
	IOptions<AnthropicOptions> anthropicOptions,
	ILogger<ClaudeIngredientExtractorClient> logger)
	: IClaudeIngredientExtractorClient
{
	private static readonly Regex JsonFenceRegex = new(
		"```(?:json)?\\s*(?<payload>[\\s\\S]*?)\\s*```",
		RegexOptions.IgnoreCase | RegexOptions.Compiled,
		TimeSpan.FromSeconds(1));

	public async Task<Result<ImportedIngredientSet>> ExtractIngredientsAsync(
		string recipeText,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(recipeText))
		{
			return Result<ImportedIngredientSet>.Failure(new AppError(
				ErrorCodes.ValidationFailed,
				"Recipe page text is required for ingredient import."));
		}

		var options = anthropicOptions.Value;
		if (string.IsNullOrWhiteSpace(options.ApiKey))
		{
			return Result<ImportedIngredientSet>.Failure(new AppError(
				ErrorCodes.ValidationFailed,
				"Ingredient import is not configured. Missing Anthropic API key."));
		}

		try
		{
			var prompt = BuildPrompt(recipeText, Math.Max(1, options.MaxIngredients));
			var messageParameters = new MessageParameters
			{
				Messages = [new Message(RoleType.User, prompt)],
				Model = AnthropicModels.Claude45Haiku,
				MaxTokens = Math.Max(300, options.MaxOutputTokens),
				Temperature = 0,
				Stream = false
			};

			var httpClient = httpClientFactory.CreateClient(AnthropicOptions.HttpClientName);
			using var client = new AnthropicClient(new APIAuthentication(options.ApiKey), httpClient);
			var response = await client.Messages.GetClaudeMessageAsync(messageParameters, cancellationToken);
			var output = response?.Message?.ToString() ?? string.Empty;

			return ParseModelOutput(output, options.MaxIngredients);
		}
		catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
		{
			logger.LogWarning(ex, "Timed out while calling Anthropic for ingredient import.");
			return Result<ImportedIngredientSet>.Failure(new AppError(
				ErrorCodes.ExternalServiceError,
				"Timed out while calling the ingredient import AI provider."));
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Anthropic ingredient import call failed.");
			return Result<ImportedIngredientSet>.Failure(new AppError(
				ErrorCodes.ExternalServiceError,
				"Failed to process ingredient import request with AI provider.",
				ex));
		}
	}

	internal static Result<ImportedIngredientSet> ParseModelOutput(string output, int maxIngredients)
	{
		var jsonPayload = ExtractJsonPayload(output);
		if (string.IsNullOrWhiteSpace(jsonPayload))
		{
			return Result<ImportedIngredientSet>.Failure(new AppError(
				ErrorCodes.ValidationFailed,
				"The AI provider did not return a valid ingredient payload."));
		}

		try
		{
			using var document = JsonDocument.Parse(jsonPayload);
			var warnings = new List<string>();
			var (ingredients, recipeName, servings) = ParseRoot(document.RootElement, warnings);

			if (ingredients.Count == 0)
			{
				return Result<ImportedIngredientSet>.Failure(new AppError(
					ErrorCodes.ValidationFailed,
					"No ingredients could be parsed from the source page."));
			}

			var ingredientLimit = Math.Max(1, maxIngredients);
			if (ingredients.Count > ingredientLimit)
			{
				warnings.Add($"Imported first {ingredientLimit} ingredients due to configured limit.");
				ingredients = ingredients.Take(ingredientLimit).ToList();
			}

			var deduplicatedWarnings = warnings
				.Select(w => w.Trim())
				.Where(w => !string.IsNullOrWhiteSpace(w))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			return Result<ImportedIngredientSet>.Success(
				new ImportedIngredientSet(ingredients, deduplicatedWarnings, recipeName, servings));
		}
		catch (JsonException ex)
		{
			return Result<ImportedIngredientSet>.Failure(new AppError(
				ErrorCodes.ValidationFailed,
				"The AI provider response was malformed and could not be parsed.",
				ex));
		}
	}

	private static (List<Ingredient> Ingredients, Option<string> RecipeName, Option<int> Servings) ParseRoot(
		JsonElement root, List<string> warnings)
	{
		if (root.ValueKind == JsonValueKind.Array)
			return (ParseIngredientArray(root, warnings), Option<string>.None(), Option<int>.None());

		if (root.ValueKind != JsonValueKind.Object)
			return ([], Option<string>.None(), Option<int>.None());

		var recipeName = ParseRecipeName(root);
		var servings = ParseServings(root);

		if (root.TryGetProperty("warnings", out var warningElement)
		    && warningElement.ValueKind == JsonValueKind.Array)
		{
			foreach (var warning in warningElement.EnumerateArray())
			{
				if (warning.ValueKind == JsonValueKind.String)
					warnings.Add(warning.GetString() ?? string.Empty);
			}
		}

		if (root.TryGetProperty("ingredients", out var ingredientElement)
		    && ingredientElement.ValueKind == JsonValueKind.Array)
		{
			return (ParseIngredientArray(ingredientElement, warnings), recipeName, servings);
		}

		var singleIngredient = TryParseIngredient(root, warnings, 1);
		return (singleIngredient is null ? [] : [singleIngredient], recipeName, servings);
	}

	private static Option<string> ParseRecipeName(JsonElement root)
	{
		if (root.TryGetProperty("recipeName", out var nameElement)
		    && nameElement.ValueKind == JsonValueKind.String)
		{
			var name = nameElement.GetString()?.Trim();
			if (!string.IsNullOrWhiteSpace(name))
				return Option<string>.Some(name);
		}

		return Option<string>.None();
	}

	private static Option<int> ParseServings(JsonElement root)
	{
		if (root.TryGetProperty("servings", out var servingsElement))
		{
			if (servingsElement.ValueKind == JsonValueKind.Number
			    && servingsElement.TryGetInt32(out var servings)
			    && servings > 0)
			{
				return Option<int>.Some(servings);
			}

			if (servingsElement.ValueKind == JsonValueKind.String)
			{
				var raw = servingsElement.GetString()?.Trim();
				if (!string.IsNullOrWhiteSpace(raw)
				    && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
				    && parsed > 0)
				{
					return Option<int>.Some(parsed);
				}
			}
		}

		return Option<int>.None();
	}

	private static List<Ingredient> ParseIngredientArray(JsonElement arrayElement, List<string> warnings)
	{
		var ingredients = new List<Ingredient>();
		var index = 0;
		foreach (var element in arrayElement.EnumerateArray())
		{
			index++;
			var ingredient = TryParseIngredient(element, warnings, index);
			if (ingredient is not null)
				ingredients.Add(ingredient);
		}

		return ingredients;
	}

	private static Ingredient? TryParseIngredient(JsonElement element, List<string> warnings, int index)
	{
		if (element.ValueKind != JsonValueKind.Object)
		{
			warnings.Add($"Skipped ingredient #{index}: expected object.");
			return null;
		}

		if (!element.TryGetProperty("name", out var nameElement)
		    || nameElement.ValueKind != JsonValueKind.String)
		{
			warnings.Add($"Skipped ingredient #{index}: missing name.");
			return null;
		}

		var name = nameElement.GetString()?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(name))
		{
			warnings.Add($"Skipped ingredient #{index}: empty name.");
			return null;
		}

		var quantity = 0m;
		if (element.TryGetProperty("quantity", out var quantityElement))
		{
			quantity = ParseQuantity(quantityElement, warnings, index);
		}

		var unit = string.Empty;
		if (element.TryGetProperty("unit", out var unitElement)
		    && unitElement.ValueKind == JsonValueKind.String)
		{
			unit = unitElement.GetString()?.Trim() ?? string.Empty;
		}

		var isPantryStaple = false;
		if (element.TryGetProperty("isPantryStaple", out var stapleElement))
		{
			if (stapleElement.ValueKind == JsonValueKind.True)
				isPantryStaple = true;
			else if (stapleElement.ValueKind == JsonValueKind.False)
				isPantryStaple = false;
		}

		return new Ingredient(name, quantity, unit, isPantryStaple);
	}

	private static decimal ParseQuantity(JsonElement quantityElement, List<string> warnings, int index)
	{
		if (quantityElement.ValueKind == JsonValueKind.Number
		    && quantityElement.TryGetDecimal(out var numericQuantity))
		{
			return numericQuantity;
		}

		if (quantityElement.ValueKind == JsonValueKind.String)
		{
			var raw = quantityElement.GetString()?.Trim();
			if (!string.IsNullOrWhiteSpace(raw)
			    && (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
			        || decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed)))
			{
				return parsed;
			}
		}

		warnings.Add($"Ingredient #{index} had an invalid quantity. Defaulted to 0.");
		return 0m;
	}

	private static string BuildPrompt(string recipeText, int maxIngredients)
	{
		return $$"""
		         You are extracting recipe information from webpage text.

		         Return ONLY valid JSON with this shape:
		         {
		           "recipeName": "string",
		           "servings": 0,
		           "ingredients": [
		             { "name": "string", "quantity": 0, "unit": "string", "isPantryStaple": false }
		           ],
		           "warnings": ["string"]
		         }

		         Rules:
		         - recipeName: the title of the recipe. Use an empty string if it cannot be determined.
		         - servings: the number of servings the recipe makes, as an integer. Use 0 if it cannot be determined.
		         - name is required for each ingredient.
		         - quantity must be a decimal number. If unknown, use 0.
		         - unit must be present as a string; use empty string when unknown.
		         - isPantryStaple: set to true for common pantry staples that most cooks already have (e.g. salt, pepper, oil, butter, sugar, flour, garlic, water, cooking spray, baking soda, baking powder, vanilla extract, vinegar, soy sauce). Set to false for everything else.
		         - Only include ingredient list items, not instructions.
		         - Include warnings for uncertain parsing or omitted entries.
		         - Return at most {{maxIngredients}} ingredients.
		         - No markdown fences, no explanation, no extra keys.

		         Webpage text:
		         {{recipeText}}
		         """;
	}

	private static string ExtractJsonPayload(string output)
	{
		if (string.IsNullOrWhiteSpace(output))
			return string.Empty;

		var trimmed = output.Trim();
		if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
			return trimmed;

		var fenceMatch = JsonFenceRegex.Match(trimmed);
		if (fenceMatch.Success)
			return fenceMatch.Groups["payload"].Value.Trim();

		var objectStart = trimmed.IndexOf('{');
		var objectEnd = trimmed.LastIndexOf('}');
		if (objectStart >= 0 && objectEnd > objectStart)
			return trimmed[objectStart..(objectEnd + 1)].Trim();

		var arrayStart = trimmed.IndexOf('[');
		var arrayEnd = trimmed.LastIndexOf(']');
		if (arrayStart >= 0 && arrayEnd > arrayStart)
			return trimmed[arrayStart..(arrayEnd + 1)].Trim();

		return string.Empty;
	}
}
