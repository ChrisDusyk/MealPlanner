using MealPlanner.Api.Data;
using MealPlanner.Api.Features.FeatureFlags.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.FeatureFlags.Queries;

/// <summary>
/// Loads a single feature flag definition. Used by the admin edit screen.
/// </summary>
public record GetFeatureFlagByKeyQuery(string Key) : IQuery<FeatureFlag>;

public class GetFeatureFlagByKeyQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetFeatureFlagByKeyQuery, FeatureFlag>
{
	public async Task<Result<FeatureFlag>> HandleAsync(
		GetFeatureFlagByKeyQuery query,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(query.Key))
		{
			return Result<FeatureFlag>.Failure(
				new Error(ErrorCodes.ValidationFailed, "A feature flag key is required."));
		}

		try
		{
			var entity = await db.FeatureFlags
				.AsNoTracking()
				.FirstOrDefaultAsync(f => f.Key == query.Key, cancellationToken);

			return entity is null
				? Result<FeatureFlag>.Failure(
					new Error(ErrorCodes.NotFound, $"Feature flag '{query.Key}' was not found."))
				: Result<FeatureFlag>.Success(FeatureFlagMapper.ToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<FeatureFlag>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve the feature flag.", ex));
		}
	}
}
