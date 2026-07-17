using MealPlanner.Api.Features.Families;
using MealPlanner.Api.Features.Families.Queries;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Families.Queries;

public class GetMyFamilyTests
{
	[Fact]
	public async Task HandleAsync_ReturnsFamilyWithMembersAndInvitations_ForOwner()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1");
			FamilyTestData.SeedUser(db, "owner-1", "Owner", "owner@example.com");
			FamilyTestData.SeedUser(db, "member-1", "Member", "member@example.com");
			FamilyTestData.SeedUser(db, "invitee-1", "Invitee", "invitee@example.com");
			db.FamilyInvitations.Add(new MealPlanner.Api.Data.Entities.FamilyInvitationEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = TestIds.Family("fam"),
				InviterUserId = "owner-1",
				InviteeUserId = "invitee-1",
				CreatedAt = DateTime.UtcNow
			});
		});

		var handler = new GetMyFamilyQueryHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(new GetMyFamilyQuery("owner-1"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		var family = result.Value!;
		Assert.True(family.IsOwner);
		Assert.Equal(2, family.Family.Members.Count);
		Assert.True(family.Family.Members[0].IsOwner);
		Assert.Equal("Owner", family.Family.Members[0].Name);
		Assert.Single(family.PendingInvitations);
		Assert.Equal("Invitee", family.PendingInvitations[0].InviteeName);
	}

	[Fact]
	public async Task HandleAsync_ProvisionsPersonalFamily_WhenUserHasNone()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedUser(db, "solo-1", "Solo");
		});

		var handler = new GetMyFamilyQueryHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(new GetMyFamilyQuery("solo-1"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.True(result.Value!.IsOwner);
		Assert.Single(result.Value.Family.Members);
		Assert.Equal("Solo's Family", result.Value.Family.Name);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenUserIdMissing()
	{
		var context = TestDbContextFactory.CreateContext();
		var handler = new GetMyFamilyQueryHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(new GetMyFamilyQuery(""), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}
}
