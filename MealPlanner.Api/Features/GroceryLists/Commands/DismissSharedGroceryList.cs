using MealPlanner.Api.Data;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command for the recipient to dismiss a shared grocery list.
/// </summary>
public record DismissSharedGroceryListCommand(
	string RecipientUserId,
	string ShareId
) : ICommand<Unit>;

/// <summary>
/// Handles dismissing a shared grocery list by setting DismissedByRecipient = true.
/// Only the recipient can dismiss.
/// </summary>
public class DismissSharedGroceryListCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<DismissSharedGroceryListCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		DismissSharedGroceryListCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.ShareId))
			return Result<Unit>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Share ID is required."));

		if (!Guid.TryParse(command.ShareId, out var shareGuid))
			return Result<Unit>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Share ID is invalid."));

		try
		{
			var share = await db.GroceryListShares
				.FirstOrDefaultAsync(s => s.Id == shareGuid && s.SharedWithUserId == command.RecipientUserId, cancellationToken);

			if (share is null)
				return Result<Unit>.Failure(
					new Error(ErrorCodes.NotFound, "Share not found or you are not the recipient."));

			share.DismissedByRecipient = true;
			await db.SaveChangesAsync(cancellationToken);

			return Result<Unit>.Success(Unit.Value);
		}
		catch (Exception ex)
		{
			return Result<Unit>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to dismiss shared grocery list.", ex));
		}
	}
}
