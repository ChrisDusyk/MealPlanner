using MealPlanner.Api.Data;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Families.Commands;

/// <summary>
/// Command for the invited user to decline a pending family invitation.
/// </summary>
public record DeclineFamilyInvitationCommand(string UserId, string InvitationId) : ICommand<Unit>;

public class DeclineFamilyInvitationCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<DeclineFamilyInvitationCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		DeclineFamilyInvitationCommand command,
		CancellationToken cancellationToken = default)
	{
		if (!Guid.TryParse(command.InvitationId, out var invitationId))
			return Result<Unit>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Invitation ID is invalid."));

		try
		{
			var invitation = await db.FamilyInvitations
				.FirstOrDefaultAsync(
					i => i.Id == invitationId && i.InviteeUserId == command.UserId,
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
				new Error(ErrorCodes.DatabaseError, "Failed to decline family invitation.", ex));
		}
	}
}
