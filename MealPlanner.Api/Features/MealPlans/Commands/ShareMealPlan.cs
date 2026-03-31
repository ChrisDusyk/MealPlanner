using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Queries;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.MealPlans.Commands;

/// <summary>
/// Command to share a meal plan with another user by email.
/// </summary>
public record ShareMealPlanCommand(
	string OwnerUserId,
	string SharedWithEmail,
	string WeekStart,
	SharePermission Permission
) : ICommand<MealPlanShare>;

/// <summary>
/// Handles creating a meal plan share: validates the recipient, prevents duplicates
/// and self-shares, then inserts a share entity.
/// </summary>
public class ShareMealPlanCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<ShareMealPlanCommand, MealPlanShare>
{
	public async Task<Result<MealPlanShare>> HandleAsync(
		ShareMealPlanCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.SharedWithEmail))
			return Result<MealPlanShare>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipient email is required."));

		if (string.IsNullOrWhiteSpace(command.WeekStart))
			return Result<MealPlanShare>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Week start date is required."));

		try
		{
			// Look up the owner
			var owner = await db.Users
				.FirstOrDefaultAsync(u => u.Auth0UserId == command.OwnerUserId, cancellationToken);
			if (owner is null)
				return Result<MealPlanShare>.Failure(
					new Error(ErrorCodes.NotFound, "Owner user not found."));

			// Find recipient by email (case-insensitive)
			var normalizedEmail = command.SharedWithEmail.ToUpper();
			var recipient = await db.Users
				.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToUpper() == normalizedEmail, cancellationToken);
			if (recipient is null)
				return Result<MealPlanShare>.Failure(
					new Error(ErrorCodes.NotFound, $"No user found with email '{command.SharedWithEmail}'."));

			// Prevent self-share
			if (recipient.Auth0UserId == command.OwnerUserId)
				return Result<MealPlanShare>.Failure(
					new Error(ErrorCodes.ValidationFailed, "You cannot share a meal plan with yourself."));

			// Check for existing share
			var exists = await db.MealPlanShares
				.AnyAsync(s => s.OwnerUserId == command.OwnerUserId
					&& s.SharedWithUserId == recipient.Auth0UserId
					&& s.WeekStart == command.WeekStart, cancellationToken);
			if (exists)
				return Result<MealPlanShare>.Failure(
					new Error(ErrorCodes.ValidationFailed, "This meal plan is already shared with that user."));

			// Create the share
			var entity = new MealPlanShareEntity
			{
				Id = Guid.NewGuid(),
				OwnerUserId = command.OwnerUserId,
				SharedWithUserId = recipient.Auth0UserId,
				WeekStart = command.WeekStart,
				Permission = command.Permission.ToString(),
				SharedAt = DateTime.UtcNow,
				DismissedByRecipient = false
			};

			db.MealPlanShares.Add(entity);
			await db.SaveChangesAsync(cancellationToken);

			return Result<MealPlanShare>.Success(GetMealPlanQueryHandler.MapShareToDomain(entity));
		}
		catch (DbUpdateException)
		{
			return Result<MealPlanShare>.Failure(
				new Error(ErrorCodes.ValidationFailed, "This meal plan is already shared with that user."));
		}
		catch (Exception ex)
		{
			return Result<MealPlanShare>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to share meal plan.", ex));
		}
	}
}
