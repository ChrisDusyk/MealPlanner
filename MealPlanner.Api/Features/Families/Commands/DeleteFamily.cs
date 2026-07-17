using MealPlanner.Api.Data;
using MealPlanner.Api.Features.Families.Models;
using MealPlanner.Api.Features.Families.Queries;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Families.Commands;

/// <summary>
/// Command for the owner to delete the family. Every member (including the
/// owner) is provisioned a fresh personal family carrying copies of the
/// recipes they contributed; the family's meal plans and grocery lists are
/// deleted with it.
/// </summary>
public record DeleteFamilyCommand(string OwnerUserId) : ICommand<MyFamilyResult>;

public class DeleteFamilyCommandHandler(MealPlannerDbContext db, IFamilyContextResolver familyResolver)
	: ICommandHandler<DeleteFamilyCommand, MyFamilyResult>
{
	public async Task<Result<MyFamilyResult>> HandleAsync(
		DeleteFamilyCommand command,
		CancellationToken cancellationToken = default)
	{
		var contextResult = await familyResolver.ResolveAsync(command.OwnerUserId, cancellationToken);
		if (!contextResult.IsSuccess)
			return Result<MyFamilyResult>.Failure(contextResult.Error!);

		var context = contextResult.Value!;
		if (!context.IsOwner)
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.Unauthorized, "Only the family owner can delete the family."));

		try
		{
			var memberIds = await db.FamilyGroupMembers
				.AsNoTracking()
				.Where(m => m.FamilyGroupId == context.FamilyGroupId)
				.Select(m => m.UserId)
				.ToListAsync(cancellationToken);

			Guid ownerPersonalFamilyId = Guid.Empty;
			foreach (var memberId in memberIds)
			{
				var personalFamily = await FamilyProvisioning.CreatePersonalFamilyAsync(
					db, memberId, cancellationToken);
				await FamilyRecipeMigration.CopyContributedRecipesAsync(
					db, context.FamilyGroupId, personalFamily.Id, memberId, cancellationToken);

				if (memberId == command.OwnerUserId)
					ownerPersonalFamilyId = personalFamily.Id;
			}

			await FamilyRecipeMigration.DeleteFamilyWithContentAsync(
				db, context.FamilyGroupId, cancellationToken);

			await db.SaveChangesAsync(cancellationToken);

			var newContext = new FamilyContext(ownerPersonalFamilyId, command.OwnerUserId, IsOwner: true);
			return Result<MyFamilyResult>.Success(
				await GetMyFamilyQueryHandler.LoadMyFamilyAsync(db, newContext, cancellationToken));
		}
		catch (Exception ex)
		{
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to delete family.", ex));
		}
	}
}
