using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Families;

/// <summary>
/// Shared logic for creating single-member "personal" family groups. Every
/// user always belongs to exactly one family; a personal family is created
/// on first use, when leaving a family, or when being removed from one.
/// </summary>
internal static class FamilyProvisioning
{
	/// <summary>
	/// Creates a new personal family for the given user and adds the
	/// membership row. Does NOT call SaveChanges — the caller decides when
	/// the unit of work commits.
	/// </summary>
	internal static async Task<FamilyGroupEntity> CreatePersonalFamilyAsync(
		MealPlannerDbContext db,
		string authUserId,
		CancellationToken cancellationToken)
	{
		var familyName = await ResolvePersonalFamilyNameAsync(db, authUserId, cancellationToken);
		var now = DateTime.UtcNow;

		var family = new FamilyGroupEntity
		{
			Id = Guid.NewGuid(),
			Name = familyName,
			OwnerUserId = authUserId,
			CreatedAt = now,
			UpdatedAt = now
		};
		db.FamilyGroups.Add(family);

		db.FamilyGroupMembers.Add(new FamilyGroupMemberEntity
		{
			Id = Guid.NewGuid(),
			FamilyGroupId = family.Id,
			UserId = authUserId,
			JoinedAt = now
		});

		return family;
	}

	private static async Task<string> ResolvePersonalFamilyNameAsync(
		MealPlannerDbContext db,
		string authUserId,
		CancellationToken cancellationToken)
	{
		var user = await db.Users
			.AsNoTracking()
			.FirstOrDefaultAsync(u => u.AuthUserId == authUserId, cancellationToken);

		var displayName = user?.DisplayName;
		if (string.IsNullOrWhiteSpace(displayName))
			displayName = user?.Name;

		return string.IsNullOrWhiteSpace(displayName)
			? "My Family"
			: $"{displayName.Trim()}'s Family";
	}
}
