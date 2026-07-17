using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Families;
using MealPlanner.Api.Features.Families.Commands;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Families.Commands;

public class CancelFamilyInvitationTests
{
	[Fact]
	public async Task HandleAsync_CancelsInvitation_WhenCallerIsOwner()
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

		var handler = new CancelFamilyInvitationCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new CancelFamilyInvitationCommand("owner-1", invitationId.ToString()),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Empty(context.FamilyInvitations);
	}

	[Fact]
	public async Task HandleAsync_ReturnsUnauthorized_WhenCallerIsNotOwner()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1");
		});

		var handler = new CancelFamilyInvitationCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new CancelFamilyInvitationCommand("member-1", Guid.NewGuid().ToString()),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.Unauthorized, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenInvitationMissing()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1");
		});

		var handler = new CancelFamilyInvitationCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new CancelFamilyInvitationCommand("owner-1", Guid.NewGuid().ToString()),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenInvitationIdInvalid()
	{
		var context = TestDbContextFactory.CreateContext();
		var handler = new CancelFamilyInvitationCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new CancelFamilyInvitationCommand("owner-1", "nope"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}
}
