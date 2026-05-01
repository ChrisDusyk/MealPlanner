using MealPlanner.Api.Data;
using MealPlanner.Api.Features.Catalogue.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Catalogue.Queries;

/// <summary>
/// Admin-only listing of catalogue recipes (includes drafts).
/// </summary>
public record ListAllCatalogueRecipesQuery(
	string? Search,
	bool? IsPublished
) : IQuery<IReadOnlyList<CatalogueRecipe>>;

public class ListAllCatalogueRecipesQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<ListAllCatalogueRecipesQuery, IReadOnlyList<CatalogueRecipe>>
{
	public async Task<Result<IReadOnlyList<CatalogueRecipe>>> HandleAsync(
		ListAllCatalogueRecipesQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			IQueryable<Data.Entities.CatalogueRecipeEntity> q = db.CatalogueRecipes
				.AsNoTracking()
				.Include(r => r.Tags)
					.ThenInclude(t => t.Tag);

			if (query.IsPublished.HasValue)
			{
				var published = query.IsPublished.Value;
				q = q.Where(r => r.IsPublished == published);
			}

			if (!string.IsNullOrWhiteSpace(query.Search))
			{
				if (db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
				{
					var pattern = $"%{query.Search.Trim()}%";
					q = q.Where(r =>
						EF.Functions.ILike(r.Name, pattern) ||
						EF.Functions.ILike(r.Description, pattern));
				}
				else
				{
					var search = query.Search.Trim().ToLower();
					q = q.Where(r =>
						r.Name.ToLower().Contains(search) ||
						r.Description.ToLower().Contains(search));
				}
			}

			var entities = await q
				.OrderByDescending(r => r.UpdatedAt)
				.ToListAsync(cancellationToken);

			var recipes = entities.Select(CatalogueMapper.ToDomain).ToList()
				as IReadOnlyList<CatalogueRecipe>;

			return Result<IReadOnlyList<CatalogueRecipe>>.Success(recipes);
		}
		catch (Exception ex)
		{
			return Result<IReadOnlyList<CatalogueRecipe>>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to list catalogue recipes.", ex));
		}
	}
}

/// <summary>
/// Admin-only fetch of a single catalogue recipe (includes drafts).
/// </summary>
public record GetCatalogueRecipeForAdminQuery(Guid Id) : IQuery<CatalogueRecipe>;

public class GetCatalogueRecipeForAdminQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetCatalogueRecipeForAdminQuery, CatalogueRecipe>
{
	public async Task<Result<CatalogueRecipe>> HandleAsync(
		GetCatalogueRecipeForAdminQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var entity = await db.CatalogueRecipes
				.AsNoTracking()
				.Include(r => r.Tags)
					.ThenInclude(t => t.Tag)
				.FirstOrDefaultAsync(r => r.Id == query.Id, cancellationToken);

			if (entity is null)
			{
				return Result<CatalogueRecipe>.Failure(
					new Error(ErrorCodes.NotFound, "Catalogue recipe was not found."));
			}

			return Result<CatalogueRecipe>.Success(CatalogueMapper.ToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<CatalogueRecipe>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve catalogue recipe.", ex));
		}
	}
}
