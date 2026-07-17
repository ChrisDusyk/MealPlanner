using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Families.Commands;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Families.Commands;

public class DeclineFamilyInvitationTests
{
	[Fact]
	public async Task HandleAsync_DeclinesInvitation_WhenCallerIsInvitee()
	{
		Guid invitationId = Guid.Empty;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1");
			var invitation = new FamilyInvitationEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = TestIds.Family("fam"),
				InviterUserId = "owner-1",
				InviteeUserId = "invitee-1",
				CreatedAt = DateTime.UtcNow
			};
			db.FamilyInvitations.Add(invitation);
			invitationId = invitation.Id;
		});

		var handler = new DeclineFamilyInvitationCommandHandler(context);
		var result = await handler.HandleAsync(
			new DeclineFamilyInvitationCommand("invitee-1", invitationId.ToString()),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Empty(context.FamilyInvitations);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenInvitationIsForAnotherUser()
	{
		Guid invitationId = Guid.Empty;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1");
			var invitation = new FamilyInvitationEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = TestIds.Family("fam"),
				InviterUserId = "owner-1",
				InviteeUserId = "someone-else",
				CreatedAt = DateTime.UtcNow
			};
			db.FamilyInvitations.Add(invitation);
			invitationId = invitation.Id;
		});

		var handler = new DeclineFamilyInvitationCommandHandler(context);
		var result = await handler.HandleAsync(
			new DeclineFamilyInvitationCommand("invitee-1", invitationId.ToString()),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
		Assert.Single(context.FamilyInvitations);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenInvitationIdInvalid()
	{
		var handler = new DeclineFamilyInvitationCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new DeclineFamilyInvitationCommand("invitee-1", "nope"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}
}
