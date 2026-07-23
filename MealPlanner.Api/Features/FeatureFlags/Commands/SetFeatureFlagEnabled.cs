using MealPlanner.Api.Data;
using MealPlanner.Api.Features.FeatureFlags.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.FeatureFlags.Commands;

/// <summary>
/// Toggles a feature flag's enabled state. flagd picks up the change on its
/// next HTTP-sync poll, so the effect is live without a redeploy.
/// </summary>
public record SetFeatureFlagEnabledCommand(string Key, bool Enabled) : ICommand<FeatureFlag>;

public class SetFeatureFlagEnabledCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<SetFeatureFlagEnabledCommand, FeatureFlag>
{
	public async Task<Result<FeatureFlag>> HandleAsync(
		SetFeatureFlagEnabledCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.Key))
		{
			return Result<FeatureFlag>.Failure(
				new Error(ErrorCodes.ValidationFailed, "A feature flag key is required."));
		}

		try
		{
			var entity = await db.FeatureFlags
				.FirstOrDefaultAsync(f => f.Key == command.Key, cancellationToken);

			if (entity is null)
			{
				return Result<FeatureFlag>.Failure(
					new Error(ErrorCodes.NotFound, $"Feature flag '{command.Key}' was not found."));
			}

			entity.Enabled = command.Enabled;
			entity.UpdatedAt = DateTime.UtcNow;

			await db.SaveChangesAsync(cancellationToken);

			return Result<FeatureFlag>.Success(FeatureFlagMapper.ToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<FeatureFlag>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to update the feature flag.", ex));
		}
	}
}
