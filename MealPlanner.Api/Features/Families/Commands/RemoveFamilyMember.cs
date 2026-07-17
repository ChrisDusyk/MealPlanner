using MealPlanner.Api.Data;
using MealPlanner.Api.Features.Families.Models;
using MealPlanner.Api.Features.Families.Queries;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Families.Commands;

/// <summary>
/// Command for the owner to remove a member from the family. The removed
/// member gets a fresh personal family with copies of the recipes they
/// contributed; the family keeps the originals.
/// </summary>
public record RemoveFamilyMemberCommand(string OwnerUserId, string MemberUserId) : ICommand<MyFamilyResult>;

public class RemoveFamilyMemberCommandHandler(MealPlannerDbContext db, IFamilyContextResolver familyResolver)
	: ICommandHandler<RemoveFamilyMemberCommand, MyFamilyResult>
{
	public async Task<Result<MyFamilyResult>> HandleAsync(
		RemoveFamilyMemberCommand command,
		CancellationToken cancellationToken = default)
	{
		if (command.OwnerUserId == command.MemberUserId)
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "You cannot remove yourself — leave or delete the family instead."));

		var contextResult = await familyResolver.ResolveAsync(command.OwnerUserId, cancellationToken);
		if (!contextResult.IsSuccess)
			return Result<MyFamilyResult>.Failure(contextResult.Error!);

		var context = contextResult.Value!;
		if (!context.IsOwner)
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.Unauthorized, "Only the family owner can remove members."));

		try
		{
			var membership = await db.FamilyGroupMembers
				.FirstOrDefaultAsync(
					m => m.FamilyGroupId == context.FamilyGroupId && m.UserId == command.MemberUserId,
					cancellationToken);

			if (membership is null)
				return Result<MyFamilyResult>.Failure(
					new Error(ErrorCodes.NotFound, "That user is not a member of your family."));

			db.FamilyGroupMembers.Remove(membership);

			var personalFamily = await FamilyProvisioning.CreatePersonalFamilyAsync(
				db, command.MemberUserId, cancellationToken);

			await FamilyRecipeMigration.CopyContributedRecipesAsync(
				db, context.FamilyGroupId, personalFamily.Id, command.MemberUserId, cancellationToken);

			await db.SaveChangesAsync(cancellationToken);

			return Result<MyFamilyResult>.Success(
				await GetMyFamilyQueryHandler.LoadMyFamilyAsync(db, context, cancellationToken));
		}
		catch (Exception ex)
		{
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to remove family member.", ex));
		}
	}
}
