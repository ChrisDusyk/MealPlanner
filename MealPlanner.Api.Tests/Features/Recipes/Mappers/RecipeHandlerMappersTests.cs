using MealPlanner.Api.Features.Recipes.Commands;
using MealPlanner.Api.Features.Recipes.Queries;
using MealPlanner.Api.Data.Entities;

namespace MealPlanner.Api.Tests.Features.Recipes.Mappers;

public class RecipeHandlerMappersTests
{
	private static RecipeEntity CreateEntity(string? sourceUrl)
	{
		return new RecipeEntity
		{
			Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
			UserId = "u1",
			Name = "Omelette",
			Description = "Eggs",
			Servings = 2,
			SourceUrl = sourceUrl,
			Ingredients =
			[
				new IngredientData { Name = "Egg", Quantity = 2, Unit = "pcs", IsPantryStaple = false },
				new IngredientData { Name = "Salt", Quantity = 1, Unit = "pinch", IsPantryStaple = true }
			],
			CreatedAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc)
		};
	}

	[Fact]
	public void GetAllRecipes_MapToRecipe_MapsSourceUrlSome()
	{
		var recipe = GetAllRecipesQueryHandler.MapToRecipe(CreateEntity("https://example.com/o"));

		Assert.True(recipe.SourceUrl.HasValue);
		Assert.Equal("https://example.com/o", recipe.SourceUrl.Value);
		Assert.Equal(2, recipe.Servings);
		Assert.Equal("u1", recipe.UserId);
		Assert.Equal(2, recipe.Ingredients.Count);
		Assert.False(recipe.Ingredients[0].IsPantryStaple);
		Assert.True(recipe.Ingredients[1].IsPantryStaple);
	}

	[Fact]
	public void GetRecipeById_MapToRecipe_MapsSourceUrlNone_WhenNull()
	{
		var recipe = GetRecipeByIdQueryHandler.MapToRecipe(CreateEntity(null));

		Assert.False(recipe.SourceUrl.HasValue);
		Assert.Equal(2, recipe.Ingredients.Count);
		Assert.True(recipe.Ingredients[1].IsPantryStaple);
	}

	[Fact]
	public void CreateRecipe_MapToRecipe_MapsAllFields()
	{
		var recipe = CreateRecipeCommandHandler.MapToRecipe(CreateEntity("https://example.com/o"));

		Assert.Equal("22222222-2222-2222-2222-222222222222", recipe.Id);
		Assert.Equal("Omelette", recipe.Name);
		Assert.Equal("Eggs", recipe.Description);
		Assert.Equal(2, recipe.Servings);
		Assert.False(recipe.Ingredients[0].IsPantryStaple);
		Assert.True(recipe.Ingredients[1].IsPantryStaple);
	}

	[Fact]
	public void UpdateRecipe_MapToRecipe_MapsAllFields()
	{
		var recipe = UpdateRecipeCommandHandler.MapToRecipe(CreateEntity(null));

		Assert.Equal("22222222-2222-2222-2222-222222222222", recipe.Id);
		Assert.Equal(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc), recipe.CreatedAt);
		Assert.Equal(new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc), recipe.UpdatedAt);
		Assert.False(recipe.Ingredients[0].IsPantryStaple);
		Assert.True(recipe.Ingredients[1].IsPantryStaple);
	}
}
