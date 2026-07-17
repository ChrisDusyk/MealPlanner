using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Families.Mappers;
using MealPlanner.Api.Features.Families.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Families.Commands;

/// <summary>
/// Command to invite a registered user (looked up by email) to the caller's
/// family. Owner only. The invited user must accept before joining.
/// </summary>
public record InviteToFamilyCommand(string InviterUserId, string Email) : ICommand<FamilyInvitation>;

public class InviteToFamilyCommandHandler(MealPlannerDbContext db, IFamilyContextResolver familyResolver)
	: ICommandHandler<InviteToFamilyCommand, FamilyInvitation>
{
	public async Task<Result<FamilyInvitation>> HandleAsync(
		InviteToFamilyCommand command,
		CancellationToken cancellationToken = default)
	{
		var email = command.Email?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(email))
			return Result<FamilyInvitation>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Email is required."));

		var contextResult = await familyResolver.ResolveAsync(command.InviterUserId, cancellationToken);
		if (!contextResult.IsSuccess)
			return Result<FamilyInvitation>.Failure(contextResult.Error!);

		var context = contextResult.Value!;
		if (!context.IsOwner)
			return Result<FamilyInvitation>.Failure(
				new Error(ErrorCodes.Unauthorized, "Only the family owner can send invitations."));

		try
		{
			var normalizedEmail = email.ToUpperInvariant();
			var invitee = await db.Users
				.AsNoTracking()
				.FirstOrDefaultAsync(
					u => u.Email != null && u.Email.ToUpper() == normalizedEmail,
					cancellationToken);

			if (invitee is null)
				return Result<FamilyInvitation>.Failure(
					new Error(ErrorCodes.NotFound, $"No user found with email '{email}'. They need to sign up first."));

			if (invitee.AuthUserId == command.InviterUserId)
				return Result<FamilyInvitation>.Failure(
					new Error(ErrorCodes.ValidationFailed, "You cannot invite yourself."));

			var isAlreadyMember = await db.FamilyGroupMembers
				.AsNoTracking()
				.AnyAsync(
					m => m.FamilyGroupId == context.FamilyGroupId && m.UserId == invitee.AuthUserId,
					cancellationToken);
			if (isAlreadyMember)
				return Result<FamilyInvitation>.Failure(
					new Error(ErrorCodes.ValidationFailed, "That user is already a member of your family."));

			var alreadyInvited = await db.FamilyInvitations
				.AsNoTracking()
				.AnyAsync(
					i => i.FamilyGroupId == context.FamilyGroupId && i.InviteeUserId == invitee.AuthUserId,
					cancellationToken);
			if (alreadyInvited)
				return Result<FamilyInvitation>.Failure(
					new Error(ErrorCodes.ValidationFailed, "That user already has a pending invitation."));

			var entity = new FamilyInvitationEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = context.FamilyGroupId,
				InviterUserId = command.InviterUserId,
				InviteeUserId = invitee.AuthUserId,
				CreatedAt = DateTime.UtcNow
			};
			db.FamilyInvitations.Add(entity);
			await db.SaveChangesAsync(cancellationToken);

			var family = await db.FamilyGroups
				.AsNoTracking()
				.FirstAsync(f => f.Id == context.FamilyGroupId, cancellationToken);

			var userIds = new[] { command.InviterUserId, invitee.AuthUserId };
			var usersByAuthId = await db.Users
				.AsNoTracking()
				.Where(u => userIds.Contains(u.AuthUserId))
				.ToDictionaryAsync(u => u.AuthUserId, cancellationToken);

			return Result<FamilyInvitation>.Success(
				FamilyMapper.ToInvitation(entity, family.Name, usersByAuthId));
		}
		catch (Exception ex)
		{
			return Result<FamilyInvitation>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to send family invitation.", ex));
		}
	}
}
