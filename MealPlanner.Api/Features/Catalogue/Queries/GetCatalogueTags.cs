using MealPlanner.Api.Data;
using MealPlanner.Api.Features.Catalogue.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Catalogue.Queries;

/// <summary>
/// Lists all curated catalogue tags. Public to authenticated users; used to
/// populate filter pills in the browse modal and the admin recipe form.
/// </summary>
public record GetCatalogueTagsQuery : IQuery<IReadOnlyList<CatalogueTag>>;

public class GetCatalogueTagsQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetCatalogueTagsQuery, IReadOnlyList<CatalogueTag>>
{
	public async Task<Result<IReadOnlyList<CatalogueTag>>> HandleAsync(
		GetCatalogueTagsQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var entities = await db.CatalogueTags
				.AsNoTracking()
				.OrderBy(t => t.Name)
				.ToListAsync(cancellationToken);

			var tags = entities.Select(CatalogueMapper.ToDomain).ToList()
				as IReadOnlyList<CatalogueTag>;

			return Result<IReadOnlyList<CatalogueTag>>.Success(tags);
		}
		catch (Exception ex)
		{
			return Result<IReadOnlyList<CatalogueTag>>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve catalogue tags.", ex));
		}
	}
}
