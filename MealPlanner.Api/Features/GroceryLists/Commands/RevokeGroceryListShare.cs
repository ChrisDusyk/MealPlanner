using MealPlanner.Api.Data;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command for the owner to revoke a grocery list share.
/// </summary>
public record RevokeGroceryListShareCommand(
	string OwnerUserId,
	string ShareId
) : ICommand<Unit>;

/// <summary>
/// Handles revoking (deleting) a grocery list share. Only the share owner can revoke.
/// </summary>
public class RevokeGroceryListShareCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<RevokeGroceryListShareCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		RevokeGroceryListShareCommand command,
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
				.FirstOrDefaultAsync(s => s.Id == shareGuid && s.OwnerUserId == command.OwnerUserId, cancellationToken);

			if (share is null)
				return Result<Unit>.Failure(
					new Error(ErrorCodes.NotFound, "Share not found or you are not the owner."));

			db.GroceryListShares.Remove(share);
			await db.SaveChangesAsync(cancellationToken);

			return Result<Unit>.Success(Unit.Value);
		}
		catch (Exception ex)
		{
			return Result<Unit>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to revoke share.", ex));
		}
	}
}
