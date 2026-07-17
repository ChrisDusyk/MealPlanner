using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Families.Queries;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Families.Queries;

public class GetIncomingFamilyInvitationsTests
{
	[Fact]
	public async Task HandleAsync_ReturnsInvitations_WithFamilyAndInviterInfo()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1");
			FamilyTestData.SeedUser(db, "owner-1", "Owner");
			FamilyTestData.SeedUser(db, "invitee-1", "Invitee", "invitee@example.com");
			db.FamilyInvitations.Add(new FamilyInvitationEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = TestIds.Family("fam"),
				InviterUserId = "owner-1",
				InviteeUserId = "invitee-1",
				CreatedAt = DateTime.UtcNow
			});
		});

		var handler = new GetIncomingFamilyInvitationsQueryHandler(context);
		var result = await handler.HandleAsync(
			new GetIncomingFamilyInvitationsQuery("invitee-1"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		var invitation = Assert.Single(result.Value!);
		Assert.Equal("fam family", invitation.FamilyName);
		Assert.Equal("Owner", invitation.InviterName);
	}

	[Fact]
	public async Task HandleAsync_ReturnsEmptyList_WhenNoInvitations()
	{
		var handler = new GetIncomingFamilyInvitationsQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new GetIncomingFamilyInvitationsQuery("nobody"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Empty(result.Value!);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenUserIdMissing()
	{
		var handler = new GetIncomingFamilyInvitationsQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new GetIncomingFamilyInvitationsQuery(""), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}
}
