using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Catalogue.Models;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Catalogue.Commands;

/// <summary>
/// Admin command to update a catalogue recipe (does not change publish state).
/// </summary>
public record UpdateCatalogueRecipeCommand(
	Guid Id,
	string Name,
	string Description,
	int Servings,
	Option<string> SourceUrl,
	Option<string> ImageUrl,
	List<Ingredient> Ingredients,
	List<Guid> TagIds
) : ICommand<CatalogueRecipe>;

public class UpdateCatalogueRecipeCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<UpdateCatalogueRecipeCommand, CatalogueRecipe>
{
	public async Task<Result<CatalogueRecipe>> HandleAsync(
		UpdateCatalogueRecipeCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.Name))
		{
			return Result<CatalogueRecipe>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipe name is required."));
		}

		if (command.Servings < 1)
		{
			return Result<CatalogueRecipe>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Servings must be at least 1."));
		}

		try
		{
			var entity = await db.CatalogueRecipes
				.Include(r => r.Tags)
					.ThenInclude(t => t.Tag)
				.FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken);

			if (entity is null)
			{
				return Result<CatalogueRecipe>.Failure(
					new Error(ErrorCodes.NotFound, "Catalogue recipe was not found."));
			}

			var distinctTagIds = command.TagIds.Distinct().ToList();
			var tagEntities = distinctTagIds.Count > 0
				? await db.CatalogueTags
					.Where(t => distinctTagIds.Contains(t.Id))
					.ToListAsync(cancellationToken)
				: [];

			if (tagEntities.Count != distinctTagIds.Count)
			{
				return Result<CatalogueRecipe>.Failure(
					new Error(ErrorCodes.ValidationFailed, "One or more tags do not exist."));
			}

			entity.Name = command.Name;
			entity.Description = command.Description;
			entity.Servings = command.Servings;
			entity.SourceUrl = command.SourceUrl.GetValueOrNull();
			entity.ImageUrl = command.ImageUrl.GetValueOrNull();
			entity.Ingredients = CatalogueMapper.ToIngredientData(command.Ingredients);
			entity.UpdatedAt = DateTime.UtcNow;

			// Replace tag list.
			var existing = entity.Tags.ToList();
			foreach (var rt in existing)
			{
				if (!distinctTagIds.Contains(rt.TagId))
				{
					db.CatalogueRecipeTags.Remove(rt);
				}
			}
			var existingIds = existing.Select(rt => rt.TagId).ToHashSet();
			foreach (var tagId in distinctTagIds)
			{
				if (!existingIds.Contains(tagId))
				{
					entity.Tags.Add(new CatalogueRecipeTagEntity
					{
						CatalogueRecipeId = entity.Id,
						TagId = tagId
					});
				}
			}

			await db.SaveChangesAsync(cancellationToken);

			// Reload with tags for the response.
			var refreshed = await db.CatalogueRecipes
				.AsNoTracking()
				.Include(r => r.Tags)
					.ThenInclude(t => t.Tag)
				.FirstAsync(r => r.Id == entity.Id, cancellationToken);

			return Result<CatalogueRecipe>.Success(CatalogueMapper.ToDomain(refreshed));
		}
		catch (Exception ex)
		{
			return Result<CatalogueRecipe>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to update catalogue recipe.", ex));
		}
	}
}

/// <summary>
/// Admin command to set the published state of a catalogue recipe.
/// </summary>
public record SetCatalogueRecipePublishedCommand(Guid Id, bool IsPublished)
	: ICommand<CatalogueRecipe>;

public class SetCatalogueRecipePublishedCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<SetCatalogueRecipePublishedCommand, CatalogueRecipe>
{
	public async Task<Result<CatalogueRecipe>> HandleAsync(
		SetCatalogueRecipePublishedCommand command,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var entity = await db.CatalogueRecipes
				.Include(r => r.Tags)
					.ThenInclude(t => t.Tag)
				.FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken);

			if (entity is null)
			{
				return Result<CatalogueRecipe>.Failure(
					new Error(ErrorCodes.NotFound, "Catalogue recipe was not found."));
			}

			var now = DateTime.UtcNow;
			var wasPublished = entity.IsPublished;
			entity.IsPublished = command.IsPublished;
			if (command.IsPublished && !wasPublished && entity.PublishedAt is null)
			{
				entity.PublishedAt = now;
			}
			entity.UpdatedAt = now;

			await db.SaveChangesAsync(cancellationToken);

			return Result<CatalogueRecipe>.Success(CatalogueMapper.ToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<CatalogueRecipe>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to update publish state.", ex));
		}
	}
}

/// <summary>
/// Admin command to delete a catalogue recipe. Existing user-side copies are
/// retained but their <c>CatalogueRecipeId</c> link becomes orphaned.
/// </summary>
public record DeleteCatalogueRecipeCommand(Guid Id) : ICommand<Unit>;

public class DeleteCatalogueRecipeCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<DeleteCatalogueRecipeCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		DeleteCatalogueRecipeCommand command,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var entity = await db.CatalogueRecipes
				.FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken);

			if (entity is null)
			{
				return Result<Unit>.Failure(
					new Error(ErrorCodes.NotFound, "Catalogue recipe was not found."));
			}

			// Detach existing user-side copies so the FK link doesn't dangle.
			var copies = await db.Recipes
				.Where(r => r.CatalogueRecipeId == entity.Id)
				.ToListAsync(cancellationToken);
			foreach (var copy in copies)
			{
				copy.CatalogueRecipeId = null;
			}

			db.CatalogueRecipes.Remove(entity);
			await db.SaveChangesAsync(cancellationToken);

			return Result<Unit>.Success(Unit.Value);
		}
		catch (Exception ex)
		{
			return Result<Unit>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to delete catalogue recipe.", ex));
		}
	}
}
