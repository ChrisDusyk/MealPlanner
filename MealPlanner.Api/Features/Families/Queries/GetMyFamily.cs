using MealPlanner.Api.Data;
using MealPlanner.Api.Features.Families.Mappers;
using MealPlanner.Api.Features.Families.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Families.Queries;

/// <summary>
/// Query to retrieve the caller's family with its members and (for the owner)
/// the family's pending outgoing invitations. Auto-provisions a personal
/// family when the user does not belong to one yet.
/// </summary>
public record GetMyFamilyQuery(string UserId) : IQuery<MyFamilyResult>;

public class GetMyFamilyQueryHandler(MealPlannerDbContext db, IFamilyContextResolver familyResolver)
	: IQueryHandler<GetMyFamilyQuery, MyFamilyResult>
{
	public async Task<Result<MyFamilyResult>> HandleAsync(
		GetMyFamilyQuery query,
		CancellationToken cancellationToken = default)
	{
		var contextResult = await familyResolver.ResolveAsync(query.UserId, cancellationToken);
		if (!contextResult.IsSuccess)
			return Result<MyFamilyResult>.Failure(contextResult.Error!);

		var context = contextResult.Value!;

		try
		{
			return Result<MyFamilyResult>.Success(
				await LoadMyFamilyAsync(db, context, cancellationToken));
		}
		catch (Exception ex)
		{
			return Result<MyFamilyResult>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve family.", ex));
		}
	}

	/// <summary>
	/// Loads the full family aggregate for a resolved family context.
	/// Shared with command handlers that return the updated family state.
	/// </summary>
	internal static async Task<MyFamilyResult> LoadMyFamilyAsync(
		MealPlannerDbContext db,
		FamilyContext context,
		CancellationToken cancellationToken)
	{
		var family = await db.FamilyGroups
			.AsNoTracking()
			.FirstAsync(f => f.Id == context.FamilyGroupId, cancellationToken);

		var members = await db.FamilyGroupMembers
			.AsNoTracking()
			.Where(m => m.FamilyGroupId == family.Id)
			.ToListAsync(cancellationToken);

		var invitations = await db.FamilyInvitations
			.AsNoTracking()
			.Where(i => i.FamilyGroupId == family.Id)
			.OrderBy(i => i.CreatedAt)
			.ToListAsync(cancellationToken);

		var userIds = members.Select(m => m.UserId)
			.Concat(invitations.Select(i => i.InviterUserId))
			.Concat(invitations.Select(i => i.InviteeUserId))
			.Distinct()
			.ToList();

		var usersByAuthId = await db.Users
			.AsNoTracking()
			.Where(u => userIds.Contains(u.AuthUserId))
			.ToDictionaryAsync(u => u.AuthUserId, cancellationToken);

		return new MyFamilyResult(
			Family: FamilyMapper.ToDomain(family, members, usersByAuthId),
			IsOwner: context.IsOwner,
			PendingInvitations: invitations
				.Select(i => FamilyMapper.ToInvitation(i, family.Name, usersByAuthId))
				.ToList()
		);
	}
}
