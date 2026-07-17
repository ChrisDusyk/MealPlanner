using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Tests.TestUtilities;
using MealPlanner.Api.Features.Catalogue.Queries;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Catalogue.Queries;

public class GetCatalogueRecipeByIdTests
{
	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenMissing()
	{
		var db = TestDbContextFactory.CreateContext();
		var handler = new GetCatalogueRecipeByIdQueryHandler(db);

		var result = await handler.HandleAsync(
			new GetCatalogueRecipeByIdQuery(Guid.NewGuid(), TestIds.Family("u1")),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenUnpublished()
	{
		var id = Guid.NewGuid();
		var db = TestDbContextFactory.CreateContext(ctx => ctx.CatalogueRecipes.Add(new CatalogueRecipeEntity
		{
			Id = id, Name = "Draft", Description = "", Servings = 1, IsPublished = false,
			CreatedByUserId = "admin", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
		}));

		var handler = new GetCatalogueRecipeByIdQueryHandler(db);
		var result = await handler.HandleAsync(
			new GetCatalogueRecipeByIdQuery(id, TestIds.Family("u1")),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsRecipeAndAlreadyAddedFlag()
	{
		var id = Guid.NewGuid();
		var db = TestDbContextFactory.CreateContext(ctx =>
		{
			ctx.CatalogueRecipes.Add(new CatalogueRecipeEntity
			{
				Id = id, Name = "Pub", Description = "", Servings = 1, IsPublished = true,
				CreatedByUserId = "admin", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
			});
			ctx.Recipes.Add(new RecipeEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = TestIds.Family("u1"),
				ContributedByUserId = "u1",
				Name = "Pub",
				Description = "",
				Servings = 1,
				CatalogueRecipeId = id,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
		});

		var handler = new GetCatalogueRecipeByIdQueryHandler(db);
		var result = await handler.HandleAsync(
			new GetCatalogueRecipeByIdQuery(id, TestIds.Family("u1")),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.True(result.Value.AlreadyAdded);
		Assert.Equal("Pub", result.Value.Recipe.Name);
	}
}

public class GetCatalogueTagsTests
{
	[Fact]
	public async Task HandleAsync_ReturnsTagsOrderedByName()
	{
		var db = TestDbContextFactory.CreateContext(ctx =>
		{
			ctx.CatalogueTags.AddRange(
				new CatalogueTagEntity { Id = Guid.NewGuid(), Slug = "z", Name = "Zebra", CreatedAt = DateTime.UtcNow },
				new CatalogueTagEntity { Id = Guid.NewGuid(), Slug = "a", Name = "Apple", CreatedAt = DateTime.UtcNow });
		});

		var handler = new GetCatalogueTagsQueryHandler(db);
		var result = await handler.HandleAsync(new GetCatalogueTagsQuery(), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal(["Apple", "Zebra"], result.Value!.Select(t => t.Name));
	}
}

public class ListAllCatalogueRecipesTests
{
	[Fact]
	public async Task HandleAsync_IncludesDraftsByDefault()
	{
		var db = TestDbContextFactory.CreateContext(ctx =>
		{
			ctx.CatalogueRecipes.AddRange(
				new CatalogueRecipeEntity { Id = Guid.NewGuid(), Name = "Pub", Description = "", Servings = 1, IsPublished = true, CreatedByUserId = "a", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
				new CatalogueRecipeEntity { Id = Guid.NewGuid(), Name = "Draft", Description = "", Servings = 1, IsPublished = false, CreatedByUserId = "a", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
		});

		var handler = new ListAllCatalogueRecipesQueryHandler(db);
		var result = await handler.HandleAsync(
			new ListAllCatalogueRecipesQuery(null, null), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal(2, result.Value!.Count);
	}

	[Fact]
	public async Task HandleAsync_FiltersByPublishedFlag()
	{
		var db = TestDbContextFactory.CreateContext(ctx =>
		{
			ctx.CatalogueRecipes.AddRange(
				new CatalogueRecipeEntity { Id = Guid.NewGuid(), Name = "Pub", Description = "", Servings = 1, IsPublished = true, CreatedByUserId = "a", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
				new CatalogueRecipeEntity { Id = Guid.NewGuid(), Name = "Draft", Description = "", Servings = 1, IsPublished = false, CreatedByUserId = "a", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
		});

		var handler = new ListAllCatalogueRecipesQueryHandler(db);
		var result = await handler.HandleAsync(
			new ListAllCatalogueRecipesQuery(null, false), TestContext.Current.CancellationToken);

		Assert.Single(result.Value!);
		Assert.Equal("Draft", result.Value![0].Name);
	}
}
