using MealPlanner.Api.Data;
using MealPlanner.Api.Features.Families.Mappers;
using MealPlanner.Api.Features.Families.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Families.Queries;

/// <summary>
/// Query to list pending family invitations addressed to the caller.
/// </summary>
public record GetIncomingFamilyInvitationsQuery(string UserId) : IQuery<List<FamilyInvitation>>;

public class GetIncomingFamilyInvitationsQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetIncomingFamilyInvitationsQuery, List<FamilyInvitation>>
{
	public async Task<Result<List<FamilyInvitation>>> HandleAsync(
		GetIncomingFamilyInvitationsQuery query,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(query.UserId))
			return Result<List<FamilyInvitation>>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Auth user ID is required."));

		try
		{
			var invitations = await db.FamilyInvitations
				.AsNoTracking()
				.Where(i => i.InviteeUserId == query.UserId)
				.OrderBy(i => i.CreatedAt)
				.ToListAsync(cancellationToken);

			if (invitations.Count == 0)
				return Result<List<FamilyInvitation>>.Success([]);

			var familyIds = invitations.Select(i => i.FamilyGroupId).Distinct().ToList();
			var familyNames = await db.FamilyGroups
				.AsNoTracking()
				.Where(f => familyIds.Contains(f.Id))
				.ToDictionaryAsync(f => f.Id, f => f.Name, cancellationToken);

			var userIds = invitations.Select(i => i.InviterUserId)
				.Concat(invitations.Select(i => i.InviteeUserId))
				.Distinct()
				.ToList();
			var usersByAuthId = await db.Users
				.AsNoTracking()
				.Where(u => userIds.Contains(u.AuthUserId))
				.ToDictionaryAsync(u => u.AuthUserId, cancellationToken);

			return Result<List<FamilyInvitation>>.Success(invitations
				.Select(i => FamilyMapper.ToInvitation(
					i,
					familyNames.TryGetValue(i.FamilyGroupId, out var name) ? name : "Family",
					usersByAuthId))
				.ToList());
		}
		catch (Exception ex)
		{
			return Result<List<FamilyInvitation>>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve family invitations.", ex));
		}
	}
}
