using MealPlanner.Api.Data;
using MealPlanner.Api.Features.Families.Models;
using MealPlanner.Api.Features.Families.Queries;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Families.Commands;

/// <summary>
/// Command for the invited user to accept a family invitation. Accepting
/// moves the user out of their current family into the inviting one:
/// recipes they contributed are carried into the new family, and their old
/// family is deleted (with its meal plans and grocery lists) when it has no
/// members left.
/// </summary>
public record AcceptFamilyInvitationCommand(string UserId, string InvitationId) : ICommand<MyFamilyResult>;

public class AcceptFamilyInvitationCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<AcceptFamilyInvitationCommand, MyFamilyResult>
{
	public async Task<Result<MyFamilyResult>> HandleAsync(
		AcceptFamilyInvitationCommand command,
		CancellationToken cancellationToken = default)
	{
		if (!Guid.TryParse(command.InvitationId, out var invitationId))
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Invitation ID is invalid."));

		try
		{
			var invitation = await db.FamilyInvitations
				.FirstOrDefaultAsync(
					i => i.Id == invitationId && i.InviteeUserId == command.UserId,
					cancellationToken);

			if (invitation is null)
				return Result<MyFamilyResult>.Failure(
					new Error(ErrorCodes.NotFound, "Invitation not found."));

			var newFamily = await db.FamilyGroups
				.AsNoTracking()
				.FirstOrDefaultAsync(f => f.Id == invitation.FamilyGroupId, cancellationToken);

			if (newFamily is null)
			{
				// The inviting family no longer exists; clean up the stale invite.
				db.FamilyInvitations.Remove(invitation);
				await db.SaveChangesAsync(cancellationToken);
				return Result<MyFamilyResult>.Failure(
					new Error(ErrorCodes.NotFound, "The inviting family no longer exists."));
			}

			var membership = await db.FamilyGroupMembers
				.FirstOrDefaultAsync(m => m.UserId == command.UserId, cancellationToken);

			var now = DateTime.UtcNow;

			if (membership is not null)
			{
				var oldFamilyId = membership.FamilyGroupId;
				var oldFamily = await db.FamilyGroups
					.AsNoTracking()
					.FirstOrDefaultAsync(f => f.Id == oldFamilyId, cancellationToken);

				var otherMemberCount = await db.FamilyGroupMembers
					.AsNoTracking()
					.CountAsync(
						m => m.FamilyGroupId == oldFamilyId && m.UserId != command.UserId,
						cancellationToken);

				if (oldFamily is not null && oldFamily.OwnerUserId == command.UserId && otherMemberCount > 0)
					return Result<MyFamilyResult>.Failure(
						new Error(ErrorCodes.ValidationFailed,
							"Transfer ownership of your current family before joining another one."));

				await FamilyRecipeMigration.CopyContributedRecipesAsync(
					db, oldFamilyId, invitation.FamilyGroupId, command.UserId, cancellationToken);

				// Leave the old family; the replacement membership row is added
				// below (deletes are applied before inserts, so the unique
				// index on UserId holds).
				db.FamilyGroupMembers.Remove(membership);

				// The old family is abandoned once its last member leaves.
				if (otherMemberCount == 0)
					await FamilyRecipeMigration.DeleteFamilyWithContentAsync(db, oldFamilyId, cancellationToken);
			}

			db.FamilyGroupMembers.Add(new Data.Entities.FamilyGroupMemberEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = invitation.FamilyGroupId,
				UserId = command.UserId,
				JoinedAt = now
			});

			// Remove the accepted invitation, plus any other pending invites
			// to this user from the same family.
			var invitesToRemove = await db.FamilyInvitations
				.Where(i => i.InviteeUserId == command.UserId && i.FamilyGroupId == invitation.FamilyGroupId)
				.ToListAsync(cancellationToken);
			db.FamilyInvitations.RemoveRange(invitesToRemove);

			await db.SaveChangesAsync(cancellationToken);

			var context = new FamilyContext(
				newFamily.Id,
				newFamily.OwnerUserId,
				newFamily.OwnerUserId == command.UserId);

			return Result<MyFamilyResult>.Success(
				await GetMyFamilyQueryHandler.LoadMyFamilyAsync(db, context, cancellationToken));
		}
		catch (Exception ex)
		{
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to accept family invitation.", ex));
		}
	}
}
