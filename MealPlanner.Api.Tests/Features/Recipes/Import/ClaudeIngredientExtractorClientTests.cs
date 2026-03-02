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
}
