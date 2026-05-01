using MealPlanner.Api.Data;
using MealPlanner.Api.Features.Catalogue.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Catalogue.Queries;

public enum CatalogueSort
{
	Recent = 0,
	Popular = 1
}

/// <summary>
/// Browse the published catalogue. Used by the My Recipes browse modal.
/// </summary>
public record BrowseCatalogueQuery(
	string UserId,
	string? Search,
	IReadOnlyList<string> TagSlugs,
	CatalogueSort Sort,
	int Skip,
	int Take
) : IQuery<IReadOnlyList<CatalogueRecipeListItem>>;

public class BrowseCatalogueQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<BrowseCatalogueQuery, IReadOnlyList<CatalogueRecipeListItem>>
{
	public async Task<Result<IReadOnlyList<CatalogueRecipeListItem>>> HandleAsync(
		BrowseCatalogueQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var skip = Math.Max(0, query.Skip);
			var take = Math.Clamp(query.Take <= 0 ? 24 : query.Take, 1, 100);

			IQueryable<Data.Entities.CatalogueRecipeEntity> q = db.CatalogueRecipes
				.AsNoTracking()
				.Include(r => r.Tags)
					.ThenInclude(t => t.Tag)
				.Where(r => r.IsPublished);

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

			if (query.TagSlugs is { Count: > 0 })
			{
				var slugs = query.TagSlugs.Select(s => s.ToLowerInvariant()).ToList();
				q = q.Where(r => r.Tags.Any(t => slugs.Contains(t.Tag.Slug)));
			}

			q = query.Sort switch
			{
				CatalogueSort.Popular => q
					.OrderByDescending(r => r.AddCount)
					.ThenByDescending(r => r.PublishedAt)
					.ThenByDescending(r => r.UpdatedAt),
				_ => q
					.OrderByDescending(r => r.PublishedAt)
					.ThenByDescending(r => r.UpdatedAt)
			};

			var entities = await q.Skip(skip).Take(take).ToListAsync(cancellationToken);

			var ids = entities.Select(e => e.Id).ToList();
			var alreadyAddedIds = await db.Recipes
				.AsNoTracking()
				.Where(r => r.UserId == query.UserId
				            && r.CatalogueRecipeId != null
				            && ids.Contains(r.CatalogueRecipeId.Value))
				.Select(r => r.CatalogueRecipeId!.Value)
				.ToListAsync(cancellationToken);

			var alreadyAdded = new HashSet<Guid>(alreadyAddedIds);
			var items = entities
				.Select(e => CatalogueMapper.ToListItem(e, alreadyAdded.Contains(e.Id)))
				.ToList() as IReadOnlyList<CatalogueRecipeListItem>;

			return Result<IReadOnlyList<CatalogueRecipeListItem>>.Success(items);
		}
		catch (Exception ex)
		{
			return Result<IReadOnlyList<CatalogueRecipeListItem>>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to browse catalogue.", ex));
		}
	}
}
