using MealPlanner.Api.Data;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Families.Commands;

/// <summary>
/// Command to cancel a pending invitation sent from the caller's family.
/// Owner only.
/// </summary>
public record CancelFamilyInvitationCommand(string UserId, string InvitationId) : ICommand<Unit>;

public class CancelFamilyInvitationCommandHandler(MealPlannerDbContext db, IFamilyContextResolver familyResolver)
	: ICommandHandler<CancelFamilyInvitationCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		CancelFamilyInvitationCommand command,
		CancellationToken cancellationToken = default)
	{
		if (!Guid.TryParse(command.InvitationId, out var invitationId))
			return Result<Unit>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Invitation ID is invalid."));

		var contextResult = await familyResolver.ResolveAsync(command.UserId, cancellationToken);
		if (!contextResult.IsSuccess)
			return Result<Unit>.Failure(contextResult.Error!);

		var context = contextResult.Value!;
		if (!context.IsOwner)
			return Result<Unit>.Failure(
				new Error(ErrorCodes.Unauthorized, "Only the family owner can cancel invitations."));

		try
		{
			var invitation = await db.FamilyInvitations
				.FirstOrDefaultAsync(
					i => i.Id == invitationId && i.FamilyGroupId == context.FamilyGroupId,
					cancellationToken);

			if (invitation is null)
				return Result<Unit>.Failure(
					new Error(ErrorCodes.NotFound, "Invitation not found."));

			db.FamilyInvitations.Remove(invitation);
			await db.SaveChangesAsync(cancellationToken);

			return Result<Unit>.Success(Unit.Value);
		}
		catch (Exception ex)
		{
			return Result<Unit>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to cancel family invitation.", ex));
		}
	}
}
