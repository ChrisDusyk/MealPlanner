using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.FeatureFlags.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.FeatureFlags.Commands;

/// <summary>
/// Creates a feature flag definition. flagd picks the new flag up on its next
/// HTTP-sync poll, so no redeploy or migration is needed to introduce one.
/// </summary>
public record CreateFeatureFlagCommand(
	string Key,
	bool Enabled,
	string ValueType,
	string? DisabledVariant,
	string DefinitionJson,
	string? Description) : ICommand<FeatureFlag>;

public class CreateFeatureFlagCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<CreateFeatureFlagCommand, FeatureFlag>
{
	public async Task<Result<FeatureFlag>> HandleAsync(
		CreateFeatureFlagCommand command,
		CancellationToken cancellationToken = default)
	{
		var keyValidation = FeatureFlagDefinitionValidator.ValidateKey(command.Key);
		if (!keyValidation.IsSuccess)
		{
			return Result<FeatureFlag>.Failure(keyValidation.Error!);
		}

		var definitionValidation = FeatureFlagDefinitionValidator.ValidateDefinition(
			command.ValueType, command.DisabledVariant, command.DefinitionJson);
		if (!definitionValidation.IsSuccess)
		{
			return Result<FeatureFlag>.Failure(definitionValidation.Error!);
		}

		try
		{
			var exists = await db.FeatureFlags
				.AsNoTracking()
				.AnyAsync(f => f.Key == command.Key, cancellationToken);

			if (exists)
			{
				return Result<FeatureFlag>.Failure(
					new Error(ErrorCodes.Conflict, $"Feature flag '{command.Key}' already exists."));
			}

			var entity = new FeatureFlagEntity
			{
				Key = command.Key,
				Enabled = command.Enabled,
				ValueType = command.ValueType,
				DisabledVariant = NormalizeOptional(command.DisabledVariant),
				DefinitionJson = command.DefinitionJson,
				Description = NormalizeOptional(command.Description),
				UpdatedAt = DateTime.UtcNow
			};

			db.FeatureFlags.Add(entity);
			await db.SaveChangesAsync(cancellationToken);

			return Result<FeatureFlag>.Success(FeatureFlagMapper.ToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<FeatureFlag>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to create the feature flag.", ex));
		}
	}

	/// <summary>
	/// Collapses blank strings to null so an empty form field is stored as
	/// "absent" rather than as an empty variant name or description.
	/// </summary>
	internal static string? NormalizeOptional(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
