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
			    { "name": "Flour", "quantity": "2", "unit": "cups" },
			    { "name": "", "quantity": 1, "unit": "tsp" },
			    { "name": "Salt", "quantity": "oops", "unit": "tsp" }
			  ],
			  "warnings": ["Some measurements were inferred."]
			}
			""";

		var result = ClaudeIngredientExtractorClient.ParseModelOutput(payload, maxIngredients: 10);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal(2, result.Value.Ingredients.Count);
		Assert.Contains(result.Value.Ingredients, i => i.Name == "Flour" && i.Quantity == 2m);
		Assert.Contains(result.Value.Ingredients, i => i.Name == "Salt" && i.Quantity == 0m);
		Assert.Contains(result.Value.Warnings, w => w.Contains("inferred", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(result.Value.Warnings, w => w.Contains("invalid quantity", StringComparison.OrdinalIgnoreCase));
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
