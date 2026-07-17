using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Families.Models;

/// <summary>
/// A member of a family group, enriched with profile info from the users table.
/// </summary>
public record FamilyMember(
	string UserId,
	string Name,
	Option<string> Email,
	bool IsOwner,
	DateTime JoinedAt
);

/// <summary>
/// A family group — the unit of ownership for meal plans, grocery lists,
/// and the recipe library.
/// </summary>
public record FamilyGroup(
	string Id,
	string Name,
	string OwnerUserId,
	List<FamilyMember> Members,
	DateTime CreatedAt,
	DateTime UpdatedAt
);

/// <summary>
/// A pending invitation to join a family, enriched with display info for
/// both the inviting family and the invited user.
/// </summary>
public record FamilyInvitation(
	string Id,
	string FamilyGroupId,
	string FamilyName,
	string InviterUserId,
	string InviterName,
	string InviteeUserId,
	string InviteeName,
	Option<string> InviteeEmail,
	DateTime CreatedAt
);

/// <summary>
/// The caller's family with their role and the family's pending outgoing
/// invitations (only populated for the owner).
/// </summary>
public record MyFamilyResult(
	FamilyGroup Family,
	bool IsOwner,
	List<FamilyInvitation> PendingInvitations
);
