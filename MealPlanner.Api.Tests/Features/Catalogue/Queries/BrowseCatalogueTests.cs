using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Tests.TestUtilities;
using MealPlanner.Api.Features.Catalogue.Queries;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Catalogue.Queries;

public class BrowseCatalogueTests
{
	private static (Guid Id, CatalogueRecipeEntity Entity) MakeRecipe(
		string name,
		bool isPublished = true,
		int addCount = 0,
		DateTime? publishedAt = null,
		string description = "",
		params CatalogueTagEntity[] tags)
	{
		var id = Guid.NewGuid();
		return (id, new CatalogueRecipeEntity
		{
			Id = id,
			Name = name,
			Description = description,
			Servings = 1,
			IsPublished = isPublished,
			PublishedAt = publishedAt,
			AddCount = addCount,
			CreatedByUserId = "admin",
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow,
			Tags = tags.Select(t => new CatalogueRecipeTagEntity { TagId = t.Id, Tag = t }).ToList()
		});
	}

	[Fact]
	public async Task HandleAsync_ReturnsOnlyPublishedRecipes()
	{
		var (_, published) = MakeRecipe("Pub", isPublished: true);
		var (_, draft) = MakeRecipe("Draft", isPublished: false);
		var db = TestDbContextFactory.CreateContext(ctx =>
		{
			ctx.CatalogueRecipes.AddRange(published, draft);
		});

		var handler = new BrowseCatalogueQueryHandler(db);
		var result = await handler.HandleAsync(
			new BrowseCatalogueQuery(TestIds.Family("u1"), null, [], CatalogueSort.Recent, 0, 50),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!);
		Assert.Equal("Pub", result.Value![0].Name);
	}

	[Fact]
	public async Task HandleAsync_FiltersBySearch()
	{
		var (_, a) = MakeRecipe("Tofu Stir Fry", description: "Asian style");
		var (_, b) = MakeRecipe("Beef Chili", description: "Spicy");
		var db = TestDbContextFactory.CreateContext(ctx =>
		{
			ctx.CatalogueRecipes.AddRange(a, b);
		});

		var handler = new BrowseCatalogueQueryHandler(db);
		var result = await handler.HandleAsync(
			new BrowseCatalogueQuery(TestIds.Family("u1"), "tofu", [], CatalogueSort.Recent, 0, 50),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!);
		Assert.Equal("Tofu Stir Fry", result.Value![0].Name);
	}

	[Fact]
	public async Task HandleAsync_FiltersByTagSlug()
	{
		var vegan = new CatalogueTagEntity { Id = Guid.NewGuid(), Slug = "vegan", Name = "Vegan", CreatedAt = DateTime.UtcNow };
		var quick = new CatalogueTagEntity { Id = Guid.NewGuid(), Slug = "quick", Name = "Quick", CreatedAt = DateTime.UtcNow };
		var (_, a) = MakeRecipe("Tofu", tags: vegan);
		var (_, b) = MakeRecipe("Steak", tags: quick);
		var db = TestDbContextFactory.CreateContext(ctx =>
		{
			ctx.CatalogueTags.AddRange(vegan, quick);
			ctx.CatalogueRecipes.AddRange(a, b);
		});

		var handler = new BrowseCatalogueQueryHandler(db);
		var result = await handler.HandleAsync(
			new BrowseCatalogueQuery(TestIds.Family("u1"), null, ["vegan"], CatalogueSort.Recent, 0, 50),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!);
		Assert.Equal("Tofu", result.Value![0].Name);
	}

	[Fact]
	public async Task HandleAsync_SortsByPopular_OrdersByAddCountDesc()
	{
		var (_, low) = MakeRecipe("Low", addCount: 1);
		var (_, high) = MakeRecipe("High", addCount: 50);
		var (_, mid) = MakeRecipe("Mid", addCount: 10);
		var db = TestDbContextFactory.CreateContext(ctx =>
		{
			ctx.CatalogueRecipes.AddRange(low, high, mid);
		});

		var handler = new BrowseCatalogueQueryHandler(db);
		var result = await handler.HandleAsync(
			new BrowseCatalogueQuery(TestIds.Family("u1"), null, [], CatalogueSort.Popular, 0, 50),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		var names = result.Value!.Select(r => r.Name).ToList();
		Assert.Equal(["High", "Mid", "Low"], names);
	}

	[Fact]
	public async Task HandleAsync_ComputesAlreadyAddedFlag()
	{
		var (id, recipe) = MakeRecipe("Curry");
		var db = TestDbContextFactory.CreateContext(ctx =>
		{
			ctx.CatalogueRecipes.Add(recipe);
			ctx.Recipes.Add(new RecipeEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = TestIds.Family("u1"),
				ContributedByUserId = "u1",
				Name = "Curry",
				Description = "",
				Servings = 1,
				CatalogueRecipeId = id,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
		});

		var handler = new BrowseCatalogueQueryHandler(db);
		var asUser = await handler.HandleAsync(
			new BrowseCatalogueQuery(TestIds.Family("u1"), null, [], CatalogueSort.Recent, 0, 50),
			TestContext.Current.CancellationToken);
		var asOther = await handler.HandleAsync(
			new BrowseCatalogueQuery(TestIds.Family("u2"), null, [], CatalogueSort.Recent, 0, 50),
			TestContext.Current.CancellationToken);

		Assert.True(asUser.Value![0].AlreadyAdded);
		Assert.False(asOther.Value![0].AlreadyAdded);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseFailure_WhenContextDisposed()
	{
		var ctx = TestDbContextFactory.CreateContext();
		ctx.Dispose();
		var handler = new BrowseCatalogueQueryHandler(ctx);
		var result = await handler.HandleAsync(
			new BrowseCatalogueQuery(TestIds.Family("u1"), null, [], CatalogueSort.Recent, 0, 50),
			TestContext.Current.CancellationToken);
		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
