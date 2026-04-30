using MealPlanner.Api.Features.Catalogue.Dtos;
using MealPlanner.Api.Features.Catalogue.Models;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Catalogue.Dtos;

public class CatalogueDtosTests
{
	[Fact]
	public void CatalogueRecipeResponse_FromDomain_MapsAlreadyAddedAndOptionFields()
	{
		var domain = new CatalogueRecipe(
			Id: Guid.NewGuid(),
			Name: "X",
			Description: "Y",
			Servings: 2,
			SourceUrl: Option<string>.Some("https://x"),
			ImageUrl: Option<string>.None(),
			Ingredients: [new Ingredient("a", 1, "ea")],
			IsPublished: true,
			PublishedAt: Option<DateTime>.Some(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
			AddCount: 3,
			CreatedByUserId: "admin",
			CreatedAt: DateTime.UtcNow,
			UpdatedAt: DateTime.UtcNow,
			Tags: [new CatalogueTag(Guid.NewGuid(), "vegan", "Vegan", DateTime.UtcNow)]);

		var dto = CatalogueRecipeResponse.FromDomain(domain, alreadyAdded: true);

		Assert.True(dto.AlreadyAdded);
		Assert.Equal("https://x", dto.SourceUrl);
		Assert.Null(dto.ImageUrl);
		Assert.NotNull(dto.PublishedAt);
		Assert.Single(dto.Tags);
		Assert.Equal("vegan", dto.Tags[0].Slug);
	}

	[Fact]
	public void CatalogueRecipeListItemResponse_FromDomain_MapsListProjection()
	{
		var domain = new CatalogueRecipeListItem(
			Id: Guid.NewGuid(),
			Name: "X",
			Description: "Y",
			ImageUrl: Option<string>.Some("https://img"),
			Servings: 1,
			IngredientCount: 4,
			AddCount: 2,
			IsPublished: true,
			PublishedAt: Option<DateTime>.None(),
			Tags: [],
			AlreadyAdded: false,
			UpdatedAt: DateTime.UtcNow);

		var dto = CatalogueRecipeListItemResponse.FromDomain(domain);

		Assert.Equal("https://img", dto.ImageUrl);
		Assert.Equal(4, dto.IngredientCount);
		Assert.Null(dto.PublishedAt);
	}

	[Fact]
	public void CatalogueIngredientDto_RoundTrips()
	{
		var domain = new Ingredient("Salt", 1, "tsp", IsPantryStaple: true);
		var dto = CatalogueIngredientDto.FromDomain(domain);
		var back = dto.ToDomain();

		Assert.Equal(domain, back);
	}

	[Fact]
	public void CatalogueTagDto_FromDomain_StripsCreatedAt()
	{
		var dto = CatalogueTagDto.FromDomain(new CatalogueTag(Guid.NewGuid(), "vegan", "Vegan", DateTime.UtcNow));
		Assert.Equal("vegan", dto.Slug);
		Assert.Equal("Vegan", dto.Name);
	}
}
