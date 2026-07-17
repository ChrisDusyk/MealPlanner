using MealPlanner.Api.Data;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Families;

/// <summary>
/// The caller's family membership, resolved from their auth user id (JWT sub).
/// </summary>
public record FamilyContext(Guid FamilyGroupId, string OwnerUserId, bool IsOwner);

/// <summary>
/// Resolves the family group a user belongs to, lazily provisioning a
/// personal family when none exists yet. This is the single seam through
/// which content endpoints (meal plans, grocery lists, recipes) translate
/// the authenticated caller into a family id.
/// </summary>
public interface IFamilyContextResolver
{
	Task<Result<FamilyContext>> ResolveAsync(string authUserId, CancellationToken cancellationToken = default);
}

public sealed class FamilyContextResolver(MealPlannerDbContext db) : IFamilyContextResolver
{
	public async Task<Result<FamilyContext>> ResolveAsync(
		string authUserId,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(authUserId))
			return Result<FamilyContext>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Auth user ID is required."));

		try
		{
			var membership = await db.FamilyGroupMembers
				.AsNoTracking()
				.FirstOrDefaultAsync(m => m.UserId == authUserId, cancellationToken);

			if (membership is not null)
			{
				var family = await db.FamilyGroups
					.AsNoTracking()
					.FirstOrDefaultAsync(f => f.Id == membership.FamilyGroupId, cancellationToken);

				if (family is not null)
					return Result<FamilyContext>.Success(new FamilyContext(
						family.Id,
						family.OwnerUserId,
						family.OwnerUserId == authUserId));

				// Orphaned membership row — fall through and re-provision.
				db.FamilyGroupMembers.Remove(membership);
			}

			var personalFamily = await FamilyProvisioning.CreatePersonalFamilyAsync(db, authUserId, cancellationToken);
			await db.SaveChangesAsync(cancellationToken);

			return Result<FamilyContext>.Success(new FamilyContext(
				personalFamily.Id,
				personalFamily.OwnerUserId,
				IsOwner: true));
		}
		catch (Exception ex)
		{
			return Result<FamilyContext>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to resolve family membership.", ex));
		}
	}
}
