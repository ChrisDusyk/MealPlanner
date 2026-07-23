using MealPlanner.Api.Data;
using MealPlanner.Api.Features.FeatureFlags.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.FeatureFlags.Queries;

/// <summary>
/// Lists all feature flag definitions. Used by the admin toggle screen.
/// </summary>
public record GetFeatureFlagsQuery : IQuery<IReadOnlyList<FeatureFlag>>;

public class GetFeatureFlagsQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetFeatureFlagsQuery, IReadOnlyList<FeatureFlag>>
{
	public async Task<Result<IReadOnlyList<FeatureFlag>>> HandleAsync(
		GetFeatureFlagsQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var entities = await db.FeatureFlags
				.AsNoTracking()
				.OrderBy(f => f.Key)
				.ToListAsync(cancellationToken);

			var flags = entities.Select(FeatureFlagMapper.ToDomain).ToList()
				as IReadOnlyList<FeatureFlag>;

			return Result<IReadOnlyList<FeatureFlag>>.Success(flags);
		}
		catch (Exception ex)
		{
			return Result<IReadOnlyList<FeatureFlag>>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve feature flags.", ex));
		}
	}
}
