using MealPlanner.Api.Data;
using MealPlanner.Api.Features.Families.Models;
using MealPlanner.Api.Features.Families.Queries;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Families.Commands;

/// <summary>
/// Command for the owner to transfer family ownership to another member.
/// </summary>
public record TransferFamilyOwnershipCommand(string OwnerUserId, string NewOwnerUserId) : ICommand<MyFamilyResult>;

public class TransferFamilyOwnershipCommandHandler(MealPlannerDbContext db, IFamilyContextResolver familyResolver)
	: ICommandHandler<TransferFamilyOwnershipCommand, MyFamilyResult>
{
	public async Task<Result<MyFamilyResult>> HandleAsync(
		TransferFamilyOwnershipCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.NewOwnerUserId))
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "New owner user ID is required."));

		if (command.OwnerUserId == command.NewOwnerUserId)
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "You already own this family."));

		var contextResult = await familyResolver.ResolveAsync(command.OwnerUserId, cancellationToken);
		if (!contextResult.IsSuccess)
			return Result<MyFamilyResult>.Failure(contextResult.Error!);

		var context = contextResult.Value!;
		if (!context.IsOwner)
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.Unauthorized, "Only the family owner can transfer ownership."));

		try
		{
			var isMember = await db.FamilyGroupMembers
				.AsNoTracking()
				.AnyAsync(
					m => m.FamilyGroupId == context.FamilyGroupId && m.UserId == command.NewOwnerUserId,
					cancellationToken);

			if (!isMember)
				return Result<MyFamilyResult>.Failure(
					new Error(ErrorCodes.NotFound, "That user is not a member of your family."));

			var family = await db.FamilyGroups
				.FirstAsync(f => f.Id == context.FamilyGroupId, cancellationToken);

			family.OwnerUserId = command.NewOwnerUserId;
			family.UpdatedAt = DateTime.UtcNow;
			await db.SaveChangesAsync(cancellationToken);

			var updatedContext = new FamilyContext(family.Id, family.OwnerUserId, IsOwner: false);
			return Result<MyFamilyResult>.Success(
				await GetMyFamilyQueryHandler.LoadMyFamilyAsync(db, updatedContext, cancellationToken));
		}
		catch (Exception ex)
		{
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to transfer family ownership.", ex));
		}
	}
}
