using MealPlanner.Api.Data;
using MealPlanner.Api.Features.FeatureFlags.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.FeatureFlags.Queries;

/// <summary>
/// Produces the flagd-format flag-configuration document that flagd HTTP-syncs
/// from the API. Served (unauthenticated, token-guarded, private network) at
/// <c>GET /internal/feature-flags</c>.
/// </summary>
public record GetFeatureFlagDocumentQuery : IQuery<string>;

public class GetFeatureFlagDocumentQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetFeatureFlagDocumentQuery, string>
{
	public async Task<Result<string>> HandleAsync(
		GetFeatureFlagDocumentQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var entities = await db.FeatureFlags
				.AsNoTracking()
				.OrderBy(f => f.Key)
				.ToListAsync(cancellationToken);

			var flags = entities.Select(FeatureFlagMapper.ToDomain);
			var document = FeatureFlagMapper.ToFlagdDocument(flags);

			return Result<string>.Success(document);
		}
		catch (Exception ex)
		{
			return Result<string>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to build the feature flag document.", ex));
		}
	}
}
