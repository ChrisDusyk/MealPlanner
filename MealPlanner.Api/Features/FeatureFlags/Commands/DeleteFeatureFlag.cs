using MealPlanner.Api.Data;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.FeatureFlags.Commands;

/// <summary>
/// Deletes a feature flag definition. Note that code still evaluating the key
/// does not fail — flagd reports the flag as missing and the OpenFeature client
/// falls back to the default the caller passed, so the admin UI warns before
/// this runs.
/// </summary>
public record DeleteFeatureFlagCommand(string Key) : ICommand<Unit>;

public class DeleteFeatureFlagCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<DeleteFeatureFlagCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		DeleteFeatureFlagCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.Key))
		{
			return Result<Unit>.Failure(
				new Error(ErrorCodes.ValidationFailed, "A feature flag key is required."));
		}

		try
		{
			var entity = await db.FeatureFlags
				.FirstOrDefaultAsync(f => f.Key == command.Key, cancellationToken);

			if (entity is null)
			{
				return Result<Unit>.Failure(
					new Error(ErrorCodes.NotFound, $"Feature flag '{command.Key}' was not found."));
			}

			db.FeatureFlags.Remove(entity);
			await db.SaveChangesAsync(cancellationToken);

			return Result<Unit>.Success(Unit.Value);
		}
		catch (Exception ex)
		{
			return Result<Unit>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to delete the feature flag.", ex));
		}
	}
}
