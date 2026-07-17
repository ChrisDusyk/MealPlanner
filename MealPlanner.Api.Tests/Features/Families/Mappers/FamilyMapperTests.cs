using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Families.Mappers;

namespace MealPlanner.Api.Tests.Features.Families.Mappers;

public class FamilyMapperTests
{
	private static UserEntity User(string authId, string name, string? displayName = null, string? email = null) =>
		new()
		{
			Id = Guid.NewGuid(),
			AuthUserId = authId,
			Name = name,
			DisplayName = displayName,
			Email = email,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

	[Fact]
	public void ToDomain_MapsFamilyWithOwnerFirst()
	{
		var familyId = Guid.NewGuid();
		var family = new FamilyGroupEntity
		{
			Id = familyId,
			Name = "The Smiths",
			OwnerUserId = "owner-1",
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
		};
		var members = new List<FamilyGroupMemberEntity>
		{
			new() { Id = Guid.NewGuid(), FamilyGroupId = familyId, UserId = "member-1", JoinedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
			new() { Id = Guid.NewGuid(), FamilyGroupId = familyId, UserId = "owner-1", JoinedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc) }
		};
		var users = new Dictionary<string, UserEntity>
		{
			["owner-1"] = User("owner-1", "Owner", email: "owner@example.com"),
			["member-1"] = User("member-1", "Member")
		};

		var domain = FamilyMapper.ToDomain(family, members, users);

		Assert.Equal(familyId.ToString(), domain.Id);
		Assert.Equal("The Smiths", domain.Name);
		Assert.Equal("owner-1", domain.OwnerUserId);
		Assert.Equal(2, domain.Members.Count);
		Assert.True(domain.Members[0].IsOwner);
		Assert.Equal("owner-1", domain.Members[0].UserId);
		Assert.Equal("owner@example.com", domain.Members[0].Email.GetValueOrNull());
		Assert.False(domain.Members[1].IsOwner);
		Assert.False(domain.Members[1].Email.HasValue);
	}

	[Fact]
	public void ToMember_FallsBackToUserId_WhenUserRowMissing()
	{
		var member = new FamilyGroupMemberEntity
		{
			Id = Guid.NewGuid(),
			FamilyGroupId = Guid.NewGuid(),
			UserId = "ghost-user",
			JoinedAt = DateTime.UtcNow
		};

		var domain = FamilyMapper.ToMember(member, "owner-1", new Dictionary<string, UserEntity>());

		Assert.Equal("ghost-user", domain.UserId);
		Assert.Equal("ghost-user", domain.Name);
		Assert.False(domain.IsOwner);
	}

	[Fact]
	public void ToMember_PrefersDisplayName_OverName()
	{
		var member = new FamilyGroupMemberEntity
		{
			Id = Guid.NewGuid(),
			FamilyGroupId = Guid.NewGuid(),
			UserId = "u1",
			JoinedAt = DateTime.UtcNow
		};
		var users = new Dictionary<string, UserEntity>
		{
			["u1"] = User("u1", "Real Name", displayName: "Nickname")
		};

		var domain = FamilyMapper.ToMember(member, "u1", users);

		Assert.Equal("Nickname", domain.Name);
		Assert.True(domain.IsOwner);
	}

	[Fact]
	public void ToInvitation_MapsInviterAndInviteeInfo()
	{
		var invitation = new FamilyInvitationEntity
		{
			Id = Guid.NewGuid(),
			FamilyGroupId = Guid.NewGuid(),
			InviterUserId = "owner-1",
			InviteeUserId = "invitee-1",
			CreatedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
		};
		var users = new Dictionary<string, UserEntity>
		{
			["owner-1"] = User("owner-1", "Owner"),
			["invitee-1"] = User("invitee-1", "Invitee", email: "invitee@example.com")
		};

		var domain = FamilyMapper.ToInvitation(invitation, "The Smiths", users);

		Assert.Equal(invitation.Id.ToString(), domain.Id);
		Assert.Equal("The Smiths", domain.FamilyName);
		Assert.Equal("Owner", domain.InviterName);
		Assert.Equal("Invitee", domain.InviteeName);
		Assert.Equal("invitee@example.com", domain.InviteeEmail.GetValueOrNull());
		Assert.Equal(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), domain.CreatedAt);
	}
}
