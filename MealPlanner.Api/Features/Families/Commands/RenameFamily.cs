using MealPlanner.Api.Data;
using MealPlanner.Api.Features.Families.Models;
using MealPlanner.Api.Features.Families.Queries;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Families.Commands;

/// <summary>
/// Command to rename the caller's family. Owner only.
/// </summary>
public record RenameFamilyCommand(string UserId, string Name) : ICommand<MyFamilyResult>;

public class RenameFamilyCommandHandler(MealPlannerDbContext db, IFamilyContextResolver familyResolver)
	: ICommandHandler<RenameFamilyCommand, MyFamilyResult>
{
	public async Task<Result<MyFamilyResult>> HandleAsync(
		RenameFamilyCommand command,
		CancellationToken cancellationToken = default)
	{
		var name = command.Name?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(name))
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Family name is required."));

		if (name.Length > 100)
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Family name must be 100 characters or fewer."));

		var contextResult = await familyResolver.ResolveAsync(command.UserId, cancellationToken);
		if (!contextResult.IsSuccess)
			return Result<MyFamilyResult>.Failure(contextResult.Error!);

		var context = contextResult.Value!;
		if (!context.IsOwner)
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.Unauthorized, "Only the family owner can rename the family."));

		try
		{
			var family = await db.FamilyGroups
				.FirstAsync(f => f.Id == context.FamilyGroupId, cancellationToken);

			family.Name = name;
			family.UpdatedAt = DateTime.UtcNow;
			await db.SaveChangesAsync(cancellationToken);

			return Result<MyFamilyResult>.Success(
				await GetMyFamilyQueryHandler.LoadMyFamilyAsync(db, context, cancellationToken));
		}
		catch (Exception ex)
		{
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to rename family.", ex));
		}
	}
}
