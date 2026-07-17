using MealPlanner.Api.Features.Families.Models;

namespace MealPlanner.Api.Features.Families.Dtos;

/// <summary>
/// Response body for a single family member.
/// </summary>
public record FamilyMemberResponse(
	string UserId,
	string Name,
	string? Email,
	bool IsOwner,
	DateTime JoinedAt
)
{
	public static FamilyMemberResponse FromDomain(FamilyMember member) =>
		new(
			member.UserId,
			member.Name,
			member.Email.GetValueOrNull(),
			member.IsOwner,
			member.JoinedAt
		);
}

/// <summary>
/// Response body for a pending family invitation.
/// </summary>
public record FamilyInvitationResponse(
	string Id,
	string FamilyGroupId,
	string FamilyName,
	string InviterName,
	string InviteeName,
	string? InviteeEmail,
	DateTime CreatedAt
)
{
	public static FamilyInvitationResponse FromDomain(FamilyInvitation invitation) =>
		new(
			invitation.Id,
			invitation.FamilyGroupId,
			invitation.FamilyName,
			invitation.InviterName,
			invitation.InviteeName,
			invitation.InviteeEmail.GetValueOrNull(),
			invitation.CreatedAt
		);
}

/// <summary>
/// Response body for the caller's family, including membership and (for the
/// owner) pending outgoing invitations.
/// </summary>
public record MyFamilyResponse(
	string Id,
	string Name,
	bool IsOwner,
	List<FamilyMemberResponse> Members,
	List<FamilyInvitationResponse> PendingInvitations,
	DateTime CreatedAt,
	DateTime UpdatedAt
)
{
	public static MyFamilyResponse FromDomain(MyFamilyResult result) =>
		new(
			result.Family.Id,
			result.Family.Name,
			result.IsOwner,
			result.Family.Members.Select(FamilyMemberResponse.FromDomain).ToList(),
			(result.IsOwner ? result.PendingInvitations : [])
				.Select(FamilyInvitationResponse.FromDomain).ToList(),
			result.Family.CreatedAt,
			result.Family.UpdatedAt
		);
}

/// <summary>
/// Request body for renaming the family.
/// </summary>
public record RenameFamilyRequest(string Name);

/// <summary>
/// Request body for inviting a user to the family by email.
/// </summary>
public record InviteToFamilyRequest(string Email);

/// <summary>
/// Request body for transferring family ownership to another member.
/// </summary>
public record TransferFamilyOwnershipRequest(string NewOwnerUserId);
