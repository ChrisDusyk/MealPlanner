using MealPlanner.Api.Data;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.MealPlans.Commands;

/// <summary>
/// Command for the owner to revoke a share.
/// </summary>
public record RevokeMealPlanShareCommand(
	string OwnerUserId,
	string ShareId
) : ICommand<Unit>;

/// <summary>
/// Handles revoking (deleting) a share. Only the share owner can revoke.
/// </summary>
public class RevokeMealPlanShareCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<RevokeMealPlanShareCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		RevokeMealPlanShareCommand command,
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
			var share = await db.MealPlanShares
				.FirstOrDefaultAsync(s => s.Id == shareGuid && s.OwnerUserId == command.OwnerUserId, cancellationToken);

			if (share is null)
				return Result<Unit>.Failure(
					new Error(ErrorCodes.NotFound, "Share not found or you are not the owner."));

			db.MealPlanShares.Remove(share);
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
