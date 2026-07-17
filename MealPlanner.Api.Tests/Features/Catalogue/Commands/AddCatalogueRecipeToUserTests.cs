using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Tests.TestUtilities;
using MealPlanner.Api.Features.Catalogue.Commands;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Catalogue.Commands;

public class AddCatalogueRecipeToUserTests
{
	private static CatalogueRecipeEntity MakeRecipe(Guid id, bool published = true) => new()
	{
		Id = id,
		Name = "Cat Curry",
		Description = "yum",
		Servings = 2,
		SourceUrl = "https://x",
		IsPublished = published,
		AddCount = 0,
		CreatedByUserId = "admin",
		CreatedAt = DateTime.UtcNow,
		UpdatedAt = DateTime.UtcNow,
		Ingredients =
		[
			new IngredientData { Name = "Onion", Quantity = 1, Unit = "ea" },
			new IngredientData { Name = "Salt", Quantity = 1, Unit = "tsp", IsPantryStaple = true }
		]
	};

	[Fact]
	public async Task HandleAsync_CopiesRecipe_AndIncrementsAddCount()
	{
		var id = Guid.NewGuid();
		var dbName = $"add-cat-{Guid.NewGuid():N}";
		using (var seedCtx = TestDbContextFactory.CreateContext(dbName))
		{
			seedCtx.CatalogueRecipes.Add(MakeRecipe(id));
			await seedCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
		}
		using var ctx = TestDbContextFactory.CreateContext(dbName);

		var handler = new AddCatalogueRecipeToUserCommandHandler(ctx);
		var result = await handler.HandleAsync(
			new AddCatalogueRecipeToUserCommand(id, TestIds.Family("u1"), "u1"),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal("Cat Curry", result.Value!.Name);
		Assert.Equal(2, result.Value.Ingredients.Count);

		var stored = await ctx.Recipes.FindAsync([Guid.Parse(result.Value.Id)], TestContext.Current.CancellationToken);
		Assert.NotNull(stored);
		Assert.Equal(id, stored!.CatalogueRecipeId);

		var updated = await ctx.CatalogueRecipes.FindAsync([id], TestContext.Current.CancellationToken);
		Assert.Equal(1, updated!.AddCount);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenAlreadyAdded()
	{
		var id = Guid.NewGuid();
		var dbName = $"add-cat-{Guid.NewGuid():N}";
		using (var seedCtx = TestDbContextFactory.CreateContext(dbName))
		{
			seedCtx.CatalogueRecipes.Add(MakeRecipe(id));
			seedCtx.Recipes.Add(new RecipeEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = TestIds.Family("u1"),
				ContributedByUserId = "u1",
				Name = "Cat Curry",
				Description = "",
				Servings = 1,
				CatalogueRecipeId = id,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
			await seedCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
		}
		using var ctx = TestDbContextFactory.CreateContext(dbName);

		var handler = new AddCatalogueRecipeToUserCommandHandler(ctx);
		var result = await handler.HandleAsync(
			new AddCatalogueRecipeToUserCommand(id, TestIds.Family("u1"), "u1"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenUnpublished()
	{
		var id = Guid.NewGuid();
		var ctx = TestDbContextFactory.CreateContext(c =>
		{
			c.CatalogueRecipes.Add(MakeRecipe(id, published: false));
		});

		var handler = new AddCatalogueRecipeToUserCommandHandler(ctx);
		var result = await handler.HandleAsync(
			new AddCatalogueRecipeToUserCommand(id, TestIds.Family("u1"), "u1"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenUserIdMissing()
	{
		var ctx = TestDbContextFactory.CreateContext();
		var handler = new AddCatalogueRecipeToUserCommandHandler(ctx);

		var result = await handler.HandleAsync(
			new AddCatalogueRecipeToUserCommand(Guid.NewGuid(), TestIds.Family(""), ""),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseFailure_WhenContextDisposed()
	{
		var ctx = TestDbContextFactory.CreateContext();
		ctx.Dispose();

		var handler = new AddCatalogueRecipeToUserCommandHandler(ctx);
		var result = await handler.HandleAsync(
			new AddCatalogueRecipeToUserCommand(Guid.NewGuid(), TestIds.Family("u1"), "u1"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
