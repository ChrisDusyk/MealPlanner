using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Catalogue;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Catalogue;

public class CatalogueMapperTests
{
	[Fact]
	public void ToDomain_Recipe_MapsAllFieldsAndOptions()
	{
		var tag = new CatalogueTagEntity
		{
			Id = Guid.NewGuid(), Slug = "vegan", Name = "Vegan", CreatedAt = DateTime.UtcNow
		};
		var entity = new CatalogueRecipeEntity
		{
			Id = Guid.NewGuid(),
			Name = "Tofu",
			Description = "Yum",
			Servings = 2,
			SourceUrl = "https://x",
			ImageUrl = null,
			IsPublished = true,
			PublishedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
			AddCount = 5,
			CreatedByUserId = "admin",
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow,
			Ingredients = [new IngredientData { Name = "Tofu", Quantity = 1, Unit = "block" }],
			Tags =
			[
				new CatalogueRecipeTagEntity { TagId = tag.Id, Tag = tag }
			]
		};

		var domain = CatalogueMapper.ToDomain(entity);

		Assert.Equal("Tofu", domain.Name);
		Assert.True(domain.SourceUrl.HasValue);
		Assert.Equal("https://x", domain.SourceUrl.Value);
		Assert.False(domain.ImageUrl.HasValue);
		Assert.True(domain.PublishedAt.HasValue);
		Assert.Equal(5, domain.AddCount);
		Assert.Single(domain.Tags);
		Assert.Equal("vegan", domain.Tags[0].Slug);
		Assert.Single(domain.Ingredients);
	}

	[Fact]
	public void ToListItem_SetsAlreadyAddedAndIngredientCount()
	{
		var entity = new CatalogueRecipeEntity
		{
			Id = Guid.NewGuid(),
			Name = "X",
			Description = "Y",
			Servings = 1,
			IsPublished = true,
			PublishedAt = null,
			Ingredients =
			[
				new IngredientData { Name = "a", Quantity = 1, Unit = "" },
				new IngredientData { Name = "b", Quantity = 2, Unit = "" }
			]
		};

		var item = CatalogueMapper.ToListItem(entity, alreadyAdded: true);

		Assert.True(item.AlreadyAdded);
		Assert.Equal(2, item.IngredientCount);
		Assert.False(item.PublishedAt.HasValue);
	}

	[Fact]
	public void ToIngredientData_PreservesPantryStaple()
	{
		var ingredients = new List<Ingredient>
		{
			new("Salt", 1, "tsp", IsPantryStaple: true),
			new("Onion", 1, "ea")
		};

		var data = CatalogueMapper.ToIngredientData(ingredients);

		Assert.True(data[0].IsPantryStaple);
		Assert.False(data[1].IsPantryStaple);
	}
}
