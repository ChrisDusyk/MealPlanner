using MealPlanner.Api.Data;
using MealPlanner.Api.Features.Catalogue.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Catalogue.Queries;

/// <summary>
/// Retrieve a single published catalogue recipe with the
/// "alreadyAdded" flag computed for the current user.
/// </summary>
public record GetCatalogueRecipeByIdQuery(Guid Id, Guid FamilyGroupId)
	: IQuery<(CatalogueRecipe Recipe, bool AlreadyAdded)>;

public class GetCatalogueRecipeByIdQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetCatalogueRecipeByIdQuery, (CatalogueRecipe Recipe, bool AlreadyAdded)>
{
	public async Task<Result<(CatalogueRecipe Recipe, bool AlreadyAdded)>> HandleAsync(
		GetCatalogueRecipeByIdQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var entity = await db.CatalogueRecipes
				.AsNoTracking()
				.Include(r => r.Tags)
					.ThenInclude(t => t.Tag)
				.FirstOrDefaultAsync(r => r.Id == query.Id, cancellationToken);

			if (entity is null || !entity.IsPublished)
			{
				return Result<(CatalogueRecipe, bool)>.Failure(
					new Error(ErrorCodes.NotFound, "Catalogue recipe was not found."));
			}

			var alreadyAdded = await db.Recipes
				.AsNoTracking()
				.AnyAsync(
					r => r.FamilyGroupId == query.FamilyGroupId && r.CatalogueRecipeId == entity.Id,
					cancellationToken);

			return Result<(CatalogueRecipe, bool)>.Success(
				(CatalogueMapper.ToDomain(entity), alreadyAdded));
		}
		catch (Exception ex)
		{
			return Result<(CatalogueRecipe, bool)>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve catalogue recipe.", ex));
		}
	}
}
