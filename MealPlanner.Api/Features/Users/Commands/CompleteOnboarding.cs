using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Mappers;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Users.Commands;

/// <summary>
/// Marks the authenticated user as having completed the onboarding flow and
/// persists the profile information captured during that step.
/// </summary>
public record CompleteOnboardingCommand(
	string AuthUserId,
	Option<string> DisplayName,
	Option<string> Timezone
) : ICommand<User>;

/// <summary>
/// Handler for <see cref="CompleteOnboardingCommand"/>. Stamps
/// <see cref="UserEntity.OnboardingCompletedAt"/> using a single UTC
/// timestamp so subsequent updates (from settings, for example) are
/// idempotent without re-triggering the onboarding redirect.
/// </summary>
public class CompleteOnboardingCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<CompleteOnboardingCommand, User>
{
	public async Task<Result<User>> HandleAsync(
		CompleteOnboardingCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.AuthUserId))
			return Result<User>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Auth user ID is required."));

		try
		{
			var entity = await db.Users
				.FirstOrDefaultAsync(u => u.AuthUserId == command.AuthUserId, cancellationToken);

			if (entity is null)
				return Result<User>.Failure(
					new Error(ErrorCodes.NotFound, "User was not found."));

			var now = DateTime.UtcNow;
			var displayName = command.DisplayName.GetValueOrNull()?.Trim();
			var timezone = command.Timezone.GetValueOrNull()?.Trim();

			if (!string.IsNullOrEmpty(displayName))
			{
				entity.DisplayName = displayName;
			}

			if (!string.IsNullOrEmpty(timezone))
			{
				entity.Timezone = timezone;
			}

			// Preserve the original completion timestamp so repeated calls
			// don't reset the onboarding state.
			entity.OnboardingCompletedAt ??= now;
			entity.UpdatedAt = now;

			await db.SaveChangesAsync(cancellationToken);

			return Result<User>.Success(UserMapper.ToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<User>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to complete onboarding.", ex));
		}
	}
}
