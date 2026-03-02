using MealPlanner.Api.Features.Recipes.Import;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Recipes.Import;

public class ClaudeIngredientExtractorClientTests
{
	[Fact]
	public void ParseModelOutput_ReturnsIngredientsAndWarnings_ForPartialPayload()
	{
		const string payload =
			"""
			{
			  "recipeName": "Test Recipe",
			  "servings": 4,
			  "ingredients": [
			    { "name": "Flour", "quantity": "2", "unit": "cups", "isPantryStaple": false },
			    { "name": "", "quantity": 1, "unit": "tsp" },
			    { "name": "Salt", "quantity": "oops", "unit": "tsp", "isPantryStaple": true }
			  ],
			  "warnings": ["Some measurements were inferred."]
			}
			""";

		var result = ClaudeIngredientExtractorClient.ParseModelOutput(payload, maxIngredients: 10);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal(2, result.Value.Ingredients.Count);
		Assert.Contains(result.Value.Ingredients, i => i.Name == "Flour" && i.Quantity == 2m && !i.IsPantryStaple);
		Assert.Contains(result.Value.Ingredients, i => i.Name == "Salt" && i.Quantity == 0m && i.IsPantryStaple);
		Assert.Contains(result.Value.Warnings, w => w.Contains("inferred", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(result.Value.Warnings, w => w.Contains("invalid quantity", StringComparison.OrdinalIgnoreCase));
		Assert.True(result.Value.RecipeName.HasValue);
		Assert.Equal("Test Recipe", result.Value.RecipeName.Value);
		Assert.True(result.Value.Servings.HasValue);
		Assert.Equal(4, result.Value.Servings.Value);
	}

	[Fact]
	public void ParseModelOutput_DefaultsIsPantryStapleFalse_WhenFieldMissing()
	{
		const string payload =
			"""
			{
			  "ingredients": [
			    { "name": "Chicken", "quantity": "1", "unit": "lb" }
			  ]
			}
			""";

		var result = ClaudeIngredientExtractorClient.ParseModelOutput(payload, maxIngredients: 10);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!.Ingredients);
		Assert.False(result.Value.Ingredients[0].IsPantryStaple);
		Assert.False(result.Value.RecipeName.HasValue);
		Assert.False(result.Value.Servings.HasValue);
	}

	[Fact]
	public void ParseModelOutput_ReturnsValidationFailure_WhenNoValidIngredients()
	{
		const string payload = "{ \"ingredients\": [ { \"name\": \"\" } ] }";

		var result = ClaudeIngredientExtractorClient.ParseModelOutput(payload, maxIngredients: 10);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
	}

	[Fact]
	public void ParseModelOutput_ParsesRecipeNameAndServings_WhenPresent()
	{
		const string payload =
			"""
			{
			  "recipeName": "Chocolate Cake",
			  "servings": 8,
			  "ingredients": [
			    { "name": "Sugar", "quantity": 1, "unit": "cup", "isPantryStaple": true }
			  ]
			}
			""";

		var result = ClaudeIngredientExtractorClient.ParseModelOutput(payload, maxIngredients: 10);

		Assert.True(result.IsSuccess);
		Assert.True(result.Value!.RecipeName.HasValue);
		Assert.Equal("Chocolate Cake", result.Value.RecipeName.Value);
		Assert.True(result.Value.Servings.HasValue);
		Assert.Equal(8, result.Value.Servings.Value);
	}

	[Fact]
	public void ParseModelOutput_ReturnsNoneForNameAndServings_WhenMissingOrEmpty()
	{
		const string payload =
			"""
			{
			  "recipeName": "",
			  "servings": 0,
			  "ingredients": [
			    { "name": "Butter", "quantity": 2, "unit": "tbsp", "isPantryStaple": true }
			  ]
			}
			""";

		var result = ClaudeIngredientExtractorClient.ParseModelOutput(payload, maxIngredients: 10);

		Assert.True(result.IsSuccess);
		Assert.False(result.Value!.RecipeName.HasValue);
		Assert.False(result.Value.Servings.HasValue);
	}

	[Fact]
	public void ParseModelOutput_ReturnsNoneForServings_WhenNegative()
	{
		const string payload =
			"""
			{
			  "recipeName": "Soup",
			  "servings": -1,
			  "ingredients": [
			    { "name": "Onion", "quantity": 1, "unit": "whole" }
			  ]
			}
			""";

		var result = ClaudeIngredientExtractorClient.ParseModelOutput(payload, maxIngredients: 10);

		Assert.True(result.IsSuccess);
		Assert.True(result.Value!.RecipeName.HasValue);
		Assert.Equal("Soup", result.Value.RecipeName.Value);
		Assert.False(result.Value.Servings.HasValue);
	}

	[Fact]
	public void ParseModelOutput_ParsesServingsFromString()
	{
		const string payload =
			"""
			{
			  "servings": "6",
			  "ingredients": [
			    { "name": "Rice", "quantity": 2, "unit": "cups" }
			  ]
			}
			""";

		var result = ClaudeIngredientExtractorClient.ParseModelOutput(payload, maxIngredients: 10);

		Assert.True(result.IsSuccess);
		Assert.True(result.Value!.Servings.HasValue);
		Assert.Equal(6, result.Value.Servings.Value);
	}

	[Fact]
	public void ParseModelOutput_ReturnsNoneForNameAndServings_WhenFieldsAbsent()
	{
		const string payload =
			"""
			{
			  "ingredients": [
			    { "name": "Milk", "quantity": 1, "unit": "cup" }
			  ]
			}
			""";

		var result = ClaudeIngredientExtractorClient.ParseModelOutput(payload, maxIngredients: 10);

		Assert.True(result.IsSuccess);
		Assert.False(result.Value!.RecipeName.HasValue);
		Assert.False(result.Value.Servings.HasValue);
	}
}
