using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Families.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Families.Mappers;

/// <summary>
/// Centralised mapping from family persistence entities (plus user profile
/// rows) to the family domain records.
/// </summary>
internal static class FamilyMapper
{
	internal static FamilyGroup ToDomain(
		FamilyGroupEntity family,
		IReadOnlyList<FamilyGroupMemberEntity> members,
		IReadOnlyDictionary<string, UserEntity> usersByAuthId)
	{
		var mappedMembers = members
			.Select(m => ToMember(m, family.OwnerUserId, usersByAuthId))
			.OrderByDescending(m => m.IsOwner)
			.ThenBy(m => m.JoinedAt)
			.ToList();

		return new FamilyGroup(
			Id: family.Id.ToString(),
			Name: family.Name,
			OwnerUserId: family.OwnerUserId,
			Members: mappedMembers,
			CreatedAt: family.CreatedAt,
			UpdatedAt: family.UpdatedAt
		);
	}

	internal static FamilyMember ToMember(
		FamilyGroupMemberEntity member,
		string ownerUserId,
		IReadOnlyDictionary<string, UserEntity> usersByAuthId)
	{
		usersByAuthId.TryGetValue(member.UserId, out var user);
		return new FamilyMember(
			UserId: member.UserId,
			Name: ResolveDisplayName(user, member.UserId),
			Email: Option<string>.From(user?.Email),
			IsOwner: member.UserId == ownerUserId,
			JoinedAt: member.JoinedAt
		);
	}

	internal static FamilyInvitation ToInvitation(
		FamilyInvitationEntity invitation,
		string familyName,
		IReadOnlyDictionary<string, UserEntity> usersByAuthId)
	{
		usersByAuthId.TryGetValue(invitation.InviterUserId, out var inviter);
		usersByAuthId.TryGetValue(invitation.InviteeUserId, out var invitee);
		return new FamilyInvitation(
			Id: invitation.Id.ToString(),
			FamilyGroupId: invitation.FamilyGroupId.ToString(),
			FamilyName: familyName,
			InviterUserId: invitation.InviterUserId,
			InviterName: ResolveDisplayName(inviter, invitation.InviterUserId),
			InviteeUserId: invitation.InviteeUserId,
			InviteeName: ResolveDisplayName(invitee, invitation.InviteeUserId),
			InviteeEmail: Option<string>.From(invitee?.Email),
			CreatedAt: invitation.CreatedAt
		);
	}

	internal static string ResolveDisplayName(UserEntity? user, string fallback)
	{
		if (!string.IsNullOrWhiteSpace(user?.DisplayName))
			return user.DisplayName;
		if (!string.IsNullOrWhiteSpace(user?.Name))
			return user.Name;
		return fallback;
	}
}
