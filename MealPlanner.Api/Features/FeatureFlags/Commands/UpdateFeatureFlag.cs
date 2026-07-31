using MealPlanner.Api.Data;
using MealPlanner.Api.Features.FeatureFlags.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.FeatureFlags.Commands;

/// <summary>
/// Updates a feature flag's definition. The key is immutable — calling code
/// references flags by key, so renaming one would silently detach it from its
/// call sites; deleting and recreating is the explicit path.
/// </summary>
public record UpdateFeatureFlagCommand(
	string Key,
	bool Enabled,
	string ValueType,
	string? DisabledVariant,
	string DefinitionJson,
	string? Description) : ICommand<FeatureFlag>;

public class UpdateFeatureFlagCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<UpdateFeatureFlagCommand, FeatureFlag>
{
	public async Task<Result<FeatureFlag>> HandleAsync(
		UpdateFeatureFlagCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.Key))
		{
			return Result<FeatureFlag>.Failure(
				new Error(ErrorCodes.ValidationFailed, "A feature flag key is required."));
		}

		var definitionValidation = FeatureFlagDefinitionValidator.ValidateDefinition(
			command.ValueType, command.DisabledVariant, command.DefinitionJson);
		if (!definitionValidation.IsSuccess)
		{
			return Result<FeatureFlag>.Failure(definitionValidation.Error!);
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
			entity.ValueType = command.ValueType;
			entity.DisabledVariant = CreateFeatureFlagCommandHandler.NormalizeOptional(command.DisabledVariant);
			entity.DefinitionJson = command.DefinitionJson;
			entity.Description = CreateFeatureFlagCommandHandler.NormalizeOptional(command.Description);
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
