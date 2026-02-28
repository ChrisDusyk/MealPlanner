using MealPlanner.Api.Features.Recipes.Commands;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Features.Recipes.Queries;

namespace MealPlanner.Api.Tests.Features.Recipes.Mappers;

public class RecipeHandlerMappersTests
{
	private static RecipeDocument CreateDocument(string? sourceUrl)
	{
		return new RecipeDocument
		{
			Id = "r1",
			UserId = "u1",
			Name = "Omelette",
			Description = "Eggs",
			Servings = 2,
			SourceUrl = sourceUrl,
			Ingredients = [new IngredientDocument { Name = "Egg", Quantity = 2, Unit = "pcs" }],
			CreatedAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc)
		};
	}

	[Fact]
	public void GetAllRecipes_MapToRecipe_MapsSourceUrlSome()
	{
		var recipe = GetAllRecipesQueryHandler.MapToRecipe(CreateDocument("https://example.com/o"));

		Assert.True(recipe.SourceUrl.HasValue);
		Assert.Equal("https://example.com/o", recipe.SourceUrl.Value);
		Assert.Equal(2, recipe.Servings);
		Assert.Equal("u1", recipe.UserId);
	}

	[Fact]
	public void GetRecipeById_MapToRecipe_MapsSourceUrlNone_WhenNull()
	{
		var recipe = GetRecipeByIdQueryHandler.MapToRecipe(CreateDocument(null));

		Assert.False(recipe.SourceUrl.HasValue);
		Assert.Single(recipe.Ingredients);
	}

	[Fact]
	public void CreateRecipe_MapToRecipe_MapsAllFields()
	{
		var recipe = CreateRecipeCommandHandler.MapToRecipe(CreateDocument("https://example.com/o"));

		Assert.Equal("r1", recipe.Id);
		Assert.Equal("Omelette", recipe.Name);
		Assert.Equal("Eggs", recipe.Description);
		Assert.Equal(2, recipe.Servings);
	}

	[Fact]
	public void UpdateRecipe_MapToRecipe_MapsAllFields()
	{
		var recipe = UpdateRecipeCommandHandler.MapToRecipe(CreateDocument(null));

		Assert.Equal("r1", recipe.Id);
		Assert.Equal(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc), recipe.CreatedAt);
		Assert.Equal(new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc), recipe.UpdatedAt);
	}
}
