using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command to share a grocery list with another user by email.
/// </summary>
public record ShareGroceryListCommand(
	string OwnerUserId,
	string SharedWithEmail,
	string WeekStart,
	SharePermission Permission
) : ICommand<GroceryListShare>;

/// <summary>
/// Handles creating a grocery list share: validates the recipient, prevents duplicates
/// and self-shares, then inserts a share document.
/// </summary>
public class ShareGroceryListCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<ShareGroceryListCommand, GroceryListShare>
{
	public async Task<Result<GroceryListShare>> HandleAsync(
		ShareGroceryListCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.SharedWithEmail))
			return Result<GroceryListShare>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipient email is required."));

		if (string.IsNullOrWhiteSpace(command.WeekStart))
			return Result<GroceryListShare>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Week start date is required."));

		try
		{
			// Look up the owner to prevent self-share
			var owner = await db.Users.FirstOrDefaultAsync(u => u.Auth0UserId == command.OwnerUserId, cancellationToken);
			if (owner is null)
				return Result<GroceryListShare>.Failure(
					new Error(ErrorCodes.NotFound, "Owner user not found."));

			// Find recipient by email (case-insensitive)
			var normalizedEmail = command.SharedWithEmail.ToUpper();
			var recipient = await db.Users
				.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToUpper() == normalizedEmail, cancellationToken);
			if (recipient is null)
				return Result<GroceryListShare>.Failure(
					new Error(ErrorCodes.NotFound, $"No user found with email '{command.SharedWithEmail}'."));

			// Prevent self-share
			if (recipient.Auth0UserId == command.OwnerUserId)
				return Result<GroceryListShare>.Failure(
					new Error(ErrorCodes.ValidationFailed, "You cannot share a grocery list with yourself."));

			// Ensure a grocery list exists for this owner/week before sharing
			var groceryList = await db.GroceryLists
				.FirstOrDefaultAsync(g => g.UserId == command.OwnerUserId && g.WeekStart == command.WeekStart, cancellationToken);
			if (groceryList is null)
				return Result<GroceryListShare>.Failure(
					new Error(ErrorCodes.NotFound, "No grocery list exists for the specified week."));

			// Check for existing share
			var existing = await db.GroceryListShares
				.AnyAsync(s => s.OwnerUserId == command.OwnerUserId
					&& s.SharedWithUserId == recipient.Auth0UserId
					&& s.WeekStart == command.WeekStart, cancellationToken);
			if (existing)
				return Result<GroceryListShare>.Failure(
					new Error(ErrorCodes.ValidationFailed, "This grocery list is already shared with that user."));

			// Create the share
			var entity = new GroceryListShareEntity
			{
				Id = Guid.NewGuid(),
				OwnerUserId = command.OwnerUserId,
				SharedWithUserId = recipient.Auth0UserId,
				WeekStart = command.WeekStart,
				Permission = command.Permission.ToString(),
				SharedAt = DateTime.UtcNow,
				DismissedByRecipient = false
			};

			db.GroceryListShares.Add(entity);
			await db.SaveChangesAsync(cancellationToken);

			var share = GroceryListHelpers.MapShareToDomain(entity) with
			{
				SharedWithName = recipient.Name,
				SharedWithEmail = recipient.Email ?? string.Empty
			};

			return Result<GroceryListShare>.Success(share);
		}
		catch (DbUpdateException)
		{
			return Result<GroceryListShare>.Failure(
				new Error(ErrorCodes.ValidationFailed, "This grocery list is already shared with that user."));
		}
		catch (Exception ex)
		{
			return Result<GroceryListShare>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to share grocery list.", ex));
		}
	}

}
