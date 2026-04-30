using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Catalogue.Commands;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Catalogue.Commands;

public class CreateCatalogueRecipeTests
{
	[Fact]
	public async Task HandleAsync_CreatesRecipe_AndSetsPublishedAtWhenPublished()
	{
		var tagId = Guid.NewGuid();
		var ctx = TestDbContextFactory.CreateContext(c =>
		{
			c.CatalogueTags.Add(new CatalogueTagEntity
			{
				Id = tagId, Slug = "vegan", Name = "Vegan", CreatedAt = DateTime.UtcNow
			});
		});
		var handler = new CreateCatalogueRecipeCommandHandler(ctx);

		var command = new CreateCatalogueRecipeCommand(
			"admin", "Tofu", "yum", 2,
			Option<string>.None(), Option<string>.None(),
			[new Ingredient("Tofu", 1, "block")],
			[tagId],
			IsPublished: true);

		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.True(result.Value!.IsPublished);
		Assert.True(result.Value.PublishedAt.HasValue);
		Assert.Single(result.Value.Tags);
	}

	[Fact]
	public async Task HandleAsync_DoesNotSetPublishedAt_WhenDraft()
	{
		var ctx = TestDbContextFactory.CreateContext();
		var handler = new CreateCatalogueRecipeCommandHandler(ctx);

		var result = await handler.HandleAsync(
			new CreateCatalogueRecipeCommand(
				"admin", "Tofu", "", 1,
				Option<string>.None(), Option<string>.None(), [], [], IsPublished: false),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.False(result.Value!.PublishedAt.HasValue);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidation_WhenNameEmpty()
	{
		var handler = new CreateCatalogueRecipeCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new CreateCatalogueRecipeCommand(
				"admin", " ", "", 1,
				Option<string>.None(), Option<string>.None(), [], [], false),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidation_WhenTagDoesNotExist()
	{
		var ctx = TestDbContextFactory.CreateContext();
		var handler = new CreateCatalogueRecipeCommandHandler(ctx);

		var result = await handler.HandleAsync(
			new CreateCatalogueRecipeCommand(
				"admin", "X", "", 1,
				Option<string>.None(), Option<string>.None(), [], [Guid.NewGuid()], false),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}
}

public class UpdateCatalogueRecipeTests
{
	[Fact]
	public async Task HandleAsync_UpdatesFieldsAndReplacesTags()
	{
		var id = Guid.NewGuid();
		var oldTag = Guid.NewGuid();
		var newTag = Guid.NewGuid();
		var dbName = $"upd-cat-{Guid.NewGuid():N}";
		using (var seed = TestDbContextFactory.CreateContext(dbName))
		{
			seed.CatalogueTags.AddRange(
				new CatalogueTagEntity { Id = oldTag, Slug = "old", Name = "Old", CreatedAt = DateTime.UtcNow },
				new CatalogueTagEntity { Id = newTag, Slug = "new", Name = "New", CreatedAt = DateTime.UtcNow });
			seed.CatalogueRecipes.Add(new CatalogueRecipeEntity
			{
				Id = id, Name = "Old Name", Description = "", Servings = 1,
				IsPublished = false, CreatedByUserId = "a", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
				Tags = [new CatalogueRecipeTagEntity { CatalogueRecipeId = id, TagId = oldTag }]
			});
			await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
		}
		using var ctx = TestDbContextFactory.CreateContext(dbName);
		var handler = new UpdateCatalogueRecipeCommandHandler(ctx);

		var result = await handler.HandleAsync(
			new UpdateCatalogueRecipeCommand(
				id, "New Name", "desc", 4,
				Option<string>.Some("https://x"), Option<string>.None(),
				[new Ingredient("X", 1, "ea")],
				[newTag]),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal("New Name", result.Value!.Name);
		Assert.Equal(4, result.Value.Servings);
		Assert.Single(result.Value.Tags);
		Assert.Equal("new", result.Value.Tags[0].Slug);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenMissing()
	{
		var ctx = TestDbContextFactory.CreateContext();
		var handler = new UpdateCatalogueRecipeCommandHandler(ctx);
		var result = await handler.HandleAsync(
			new UpdateCatalogueRecipeCommand(
				Guid.NewGuid(), "X", "", 1,
				Option<string>.None(), Option<string>.None(), [], []),
			TestContext.Current.CancellationToken);
		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}
}

public class SetCatalogueRecipePublishedTests
{
	[Fact]
	public async Task HandleAsync_PublishesAndSetsPublishedAt_WhenFirstPublished()
	{
		var id = Guid.NewGuid();
		var ctx = TestDbContextFactory.CreateContext(c => c.CatalogueRecipes.Add(new CatalogueRecipeEntity
		{
			Id = id, Name = "X", Description = "", Servings = 1, IsPublished = false,
			CreatedByUserId = "a", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
		}));
		var handler = new SetCatalogueRecipePublishedCommandHandler(ctx);

		var result = await handler.HandleAsync(
			new SetCatalogueRecipePublishedCommand(id, true), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.True(result.Value!.IsPublished);
		Assert.True(result.Value.PublishedAt.HasValue);
	}

	[Fact]
	public async Task HandleAsync_KeepsOriginalPublishedAt_WhenRepublishing()
	{
		var id = Guid.NewGuid();
		var originalPublishedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var ctx = TestDbContextFactory.CreateContext(c => c.CatalogueRecipes.Add(new CatalogueRecipeEntity
		{
			Id = id, Name = "X", Description = "", Servings = 1, IsPublished = false,
			PublishedAt = originalPublishedAt,
			CreatedByUserId = "a", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
		}));
		var handler = new SetCatalogueRecipePublishedCommandHandler(ctx);

		var result = await handler.HandleAsync(
			new SetCatalogueRecipePublishedCommand(id, true), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal(originalPublishedAt, result.Value!.PublishedAt.Value);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenMissing()
	{
		var ctx = TestDbContextFactory.CreateContext();
		var handler = new SetCatalogueRecipePublishedCommandHandler(ctx);
		var result = await handler.HandleAsync(
			new SetCatalogueRecipePublishedCommand(Guid.NewGuid(), true),
			TestContext.Current.CancellationToken);
		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}
}

public class DeleteCatalogueRecipeTests
{
	[Fact]
	public async Task HandleAsync_DeletesRecipeAndDetachesUserCopies()
	{
		var id = Guid.NewGuid();
		var dbName = $"del-cat-{Guid.NewGuid():N}";
		using (var seed = TestDbContextFactory.CreateContext(dbName))
		{
			seed.CatalogueRecipes.Add(new CatalogueRecipeEntity
			{
				Id = id, Name = "X", Description = "", Servings = 1, IsPublished = true,
				CreatedByUserId = "a", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
			});
			seed.Recipes.Add(new RecipeEntity
			{
				Id = Guid.NewGuid(), UserId = "u1", Name = "X", Description = "", Servings = 1,
				CatalogueRecipeId = id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
			});
			await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
		}
		using var ctx = TestDbContextFactory.CreateContext(dbName);
		var handler = new DeleteCatalogueRecipeCommandHandler(ctx);

		var result = await handler.HandleAsync(
			new DeleteCatalogueRecipeCommand(id), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Empty(ctx.CatalogueRecipes);
		var copy = ctx.Recipes.Single();
		Assert.Null(copy.CatalogueRecipeId);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenMissing()
	{
		var handler = new DeleteCatalogueRecipeCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new DeleteCatalogueRecipeCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);
		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}
}
