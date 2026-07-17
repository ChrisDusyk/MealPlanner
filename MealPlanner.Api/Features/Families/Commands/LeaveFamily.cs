using MealPlanner.Api.Data;
using MealPlanner.Api.Features.Families.Models;
using MealPlanner.Api.Features.Families.Queries;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Families.Commands;

/// <summary>
/// Command for a member to leave their family. The departing member gets a
/// fresh personal family with copies of the recipes they contributed; the
/// family keeps the originals. The owner must transfer ownership first
/// (leaving a single-member family is a no-op and rejected).
/// </summary>
public record LeaveFamilyCommand(string UserId) : ICommand<MyFamilyResult>;

public class LeaveFamilyCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<LeaveFamilyCommand, MyFamilyResult>
{
	public async Task<Result<MyFamilyResult>> HandleAsync(
		LeaveFamilyCommand command,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var membership = await db.FamilyGroupMembers
				.FirstOrDefaultAsync(m => m.UserId == command.UserId, cancellationToken);

			if (membership is null)
				return Result<MyFamilyResult>.Failure(
					new Error(ErrorCodes.NotFound, "You are not a member of a family."));

			var familyId = membership.FamilyGroupId;
			var family = await db.FamilyGroups
				.AsNoTracking()
				.FirstOrDefaultAsync(f => f.Id == familyId, cancellationToken);

			var otherMemberCount = await db.FamilyGroupMembers
				.AsNoTracking()
				.CountAsync(m => m.FamilyGroupId == familyId && m.UserId != command.UserId, cancellationToken);

			if (otherMemberCount == 0)
				return Result<MyFamilyResult>.Failure(
					new Error(ErrorCodes.ValidationFailed,
						"You cannot leave your personal family — invite someone or join another family instead."));

			if (family is not null && family.OwnerUserId == command.UserId)
				return Result<MyFamilyResult>.Failure(
					new Error(ErrorCodes.ValidationFailed,
						"Transfer ownership to another member before leaving the family."));

			db.FamilyGroupMembers.Remove(membership);

			var personalFamily = await FamilyProvisioning.CreatePersonalFamilyAsync(
				db, command.UserId, cancellationToken);

			await FamilyRecipeMigration.CopyContributedRecipesAsync(
				db, familyId, personalFamily.Id, command.UserId, cancellationToken);

			await db.SaveChangesAsync(cancellationToken);

			var context = new FamilyContext(personalFamily.Id, command.UserId, IsOwner: true);
			return Result<MyFamilyResult>.Success(
				await GetMyFamilyQueryHandler.LoadMyFamilyAsync(db, context, cancellationToken));
		}
		catch (Exception ex)
		{
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to leave family.", ex));
		}
	}
}
