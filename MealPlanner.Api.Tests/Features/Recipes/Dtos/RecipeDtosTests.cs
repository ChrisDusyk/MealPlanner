using MealPlanner.Api.Features.Recipes.Dtos;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Recipes.Dtos;

public class RecipeDtosTests
{
	[Fact]
	public void IngredientDto_ToDomain_MapsAllFields()
	{
		var dto = new IngredientDto("Flour", 2.5m, "cups");

		var domain = dto.ToDomain();

		Assert.Equal("Flour", domain.Name);
		Assert.Equal(2.5m, domain.Quantity);
		Assert.Equal("cups", domain.Unit);
	}

	[Fact]
	public void IngredientDto_FromDomain_MapsAllFields()
	{
		var domain = new Ingredient("Salt", 1m, "tsp");

		var dto = IngredientDto.FromDomain(domain);

		Assert.Equal("Salt", dto.Name);
		Assert.Equal(1m, dto.Quantity);
		Assert.Equal("tsp", dto.Unit);
	}

	[Fact]
	public void RecipeResponse_FromDomain_MapsUrlWhenPresent()
	{
		var recipe = new Recipe(
			"r1",
			"u1",
			"Bread",
			"Baked",
			4,
			Option<string>.Some("https://example.com/bread"),
			[new Ingredient("Flour", 3, "cups")],
			new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

		var response = RecipeResponse.FromDomain(recipe);

		Assert.Equal("r1", response.Id);
		Assert.Equal("https://example.com/bread", response.SourceUrl);
		Assert.Equal(4, response.Servings);
		Assert.Single(response.Ingredients);
	}

	[Fact]
	public void RecipeResponse_FromDomain_MapsNullUrlWhenMissing()
	{
		var recipe = new Recipe(
			"r1",
			"u1",
			"Bread",
			"Baked",
			1,
			Option<string>.None(),
			[],
			new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

		var response = RecipeResponse.FromDomain(recipe);

		Assert.Null(response.SourceUrl);
		Assert.Equal(1, response.Servings);
	}
}
