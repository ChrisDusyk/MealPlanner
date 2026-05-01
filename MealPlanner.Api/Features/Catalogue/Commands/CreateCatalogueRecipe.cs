using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Catalogue.Models;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Catalogue.Commands;

/// <summary>
/// Admin command to create a catalogue recipe.
/// </summary>
public record CreateCatalogueRecipeCommand(
	string CreatedByUserId,
	string Name,
	string Description,
	int Servings,
	Option<string> SourceUrl,
	Option<string> ImageUrl,
	List<Ingredient> Ingredients,
	List<Guid> TagIds,
	bool IsPublished
) : ICommand<CatalogueRecipe>;

public class CreateCatalogueRecipeCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<CreateCatalogueRecipeCommand, CatalogueRecipe>
{
	public async Task<Result<CatalogueRecipe>> HandleAsync(
		CreateCatalogueRecipeCommand command,
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

			var now = DateTime.UtcNow;
			var entity = new CatalogueRecipeEntity
			{
				Id = Guid.NewGuid(),
				Name = command.Name,
				Description = command.Description,
				Servings = command.Servings,
				SourceUrl = command.SourceUrl.GetValueOrNull(),
				ImageUrl = command.ImageUrl.GetValueOrNull(),
				Ingredients = CatalogueMapper.ToIngredientData(command.Ingredients),
				IsPublished = command.IsPublished,
				PublishedAt = command.IsPublished ? now : null,
				CreatedByUserId = command.CreatedByUserId,
				CreatedAt = now,
				UpdatedAt = now,
				Tags = distinctTagIds
					.Select(tagId => new CatalogueRecipeTagEntity { TagId = tagId })
					.ToList()
			};

			db.CatalogueRecipes.Add(entity);
			await db.SaveChangesAsync(cancellationToken);

			// Hydrate Tag navigation for the response.
			foreach (var rt in entity.Tags)
			{
				rt.Tag = tagEntities.First(t => t.Id == rt.TagId);
			}

			return Result<CatalogueRecipe>.Success(CatalogueMapper.ToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<CatalogueRecipe>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to create catalogue recipe.", ex));
		}
	}
}
