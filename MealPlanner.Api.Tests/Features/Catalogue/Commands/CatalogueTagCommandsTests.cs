using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Catalogue.Commands;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Catalogue.Commands;

public class CreateCatalogueTagTests
{
	[Fact]
	public async Task HandleAsync_CreatesTag_WithLowercasedSlug()
	{
		var ctx = TestDbContextFactory.CreateContext();
		var handler = new CreateCatalogueTagCommandHandler(ctx);

		var result = await handler.HandleAsync(
			new CreateCatalogueTagCommand("Vegan", "Vegan"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal("vegan", result.Value!.Slug);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidation_WhenSlugInvalid()
	{
		var ctx = TestDbContextFactory.CreateContext();
		var handler = new CreateCatalogueTagCommandHandler(ctx);

		var result = await handler.HandleAsync(
			new CreateCatalogueTagCommand("invalid slug!", "Name"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidation_WhenSlugDuplicate()
	{
		var ctx = TestDbContextFactory.CreateContext(c => c.CatalogueTags.Add(new CatalogueTagEntity
		{
			Id = Guid.NewGuid(), Slug = "vegan", Name = "Vegan", CreatedAt = DateTime.UtcNow
		}));
		var handler = new CreateCatalogueTagCommandHandler(ctx);

		var result = await handler.HandleAsync(
			new CreateCatalogueTagCommand("vegan", "Vegan v2"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}
}

public class UpdateCatalogueTagTests
{
	[Fact]
	public async Task HandleAsync_UpdatesNameAndSlug()
	{
		var id = Guid.NewGuid();
		var ctx = TestDbContextFactory.CreateContext(c => c.CatalogueTags.Add(new CatalogueTagEntity
		{
			Id = id, Slug = "old", Name = "Old", CreatedAt = DateTime.UtcNow
		}));
		var handler = new UpdateCatalogueTagCommandHandler(ctx);

		var result = await handler.HandleAsync(
			new UpdateCatalogueTagCommand(id, "new-slug", "New Name"),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal("new-slug", result.Value!.Slug);
		Assert.Equal("New Name", result.Value.Name);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidation_WhenSlugCollidesWithAnotherTag()
	{
		var id = Guid.NewGuid();
		var ctx = TestDbContextFactory.CreateContext(c =>
		{
			c.CatalogueTags.AddRange(
				new CatalogueTagEntity { Id = id, Slug = "a", Name = "A", CreatedAt = DateTime.UtcNow },
				new CatalogueTagEntity { Id = Guid.NewGuid(), Slug = "b", Name = "B", CreatedAt = DateTime.UtcNow });
		});
		var handler = new UpdateCatalogueTagCommandHandler(ctx);

		var result = await handler.HandleAsync(
			new UpdateCatalogueTagCommand(id, "b", "A"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenMissing()
	{
		var handler = new UpdateCatalogueTagCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new UpdateCatalogueTagCommand(Guid.NewGuid(), "x", "X"),
			TestContext.Current.CancellationToken);
		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}
}

public class DeleteCatalogueTagTests
{
	[Fact]
	public async Task HandleAsync_DeletesTag_WhenUnused()
	{
		var id = Guid.NewGuid();
		var ctx = TestDbContextFactory.CreateContext(c => c.CatalogueTags.Add(new CatalogueTagEntity
		{
			Id = id, Slug = "x", Name = "X", CreatedAt = DateTime.UtcNow
		}));
		var handler = new DeleteCatalogueTagCommandHandler(ctx);

		var result = await handler.HandleAsync(
			new DeleteCatalogueTagCommand(id), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Empty(ctx.CatalogueTags);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidation_WhenTagInUse()
	{
		var tagId = Guid.NewGuid();
		var recipeId = Guid.NewGuid();
		var ctx = TestDbContextFactory.CreateContext(c =>
		{
			c.CatalogueTags.Add(new CatalogueTagEntity { Id = tagId, Slug = "x", Name = "X", CreatedAt = DateTime.UtcNow });
			c.CatalogueRecipes.Add(new CatalogueRecipeEntity
			{
				Id = recipeId, Name = "R", Description = "", Servings = 1, IsPublished = true,
				CreatedByUserId = "a", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
				Tags = [new CatalogueRecipeTagEntity { CatalogueRecipeId = recipeId, TagId = tagId }]
			});
		});
		var handler = new DeleteCatalogueTagCommandHandler(ctx);

		var result = await handler.HandleAsync(
			new DeleteCatalogueTagCommand(tagId), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenMissing()
	{
		var handler = new DeleteCatalogueTagCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new DeleteCatalogueTagCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);
		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}
}
