using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Families;
using MealPlanner.Api.Features.Families.Commands;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Families.Commands;

public class InviteToFamilyTests
{
	[Fact]
	public async Task HandleAsync_CreatesInvitation_WhenInviteeRegistered()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1");
			FamilyTestData.SeedUser(db, "owner-1", "Owner", "owner@example.com");
			FamilyTestData.SeedUser(db, "invitee-1", "Invitee", "invitee@example.com");
		});

		var handler = new InviteToFamilyCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new InviteToFamilyCommand("owner-1", "Invitee@Example.com"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal("invitee-1", result.Value!.InviteeUserId);
		Assert.Equal("Invitee", result.Value.InviteeName);
		Assert.Single(context.FamilyInvitations);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenEmailNotRegistered()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1");
		});

		var handler = new InviteToFamilyCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new InviteToFamilyCommand("owner-1", "stranger@example.com"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsUnauthorized_WhenCallerIsNotOwner()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1");
			FamilyTestData.SeedUser(db, "invitee-1", "Invitee", "invitee@example.com");
		});

		var handler = new InviteToFamilyCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new InviteToFamilyCommand("member-1", "invitee@example.com"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.Unauthorized, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenInvitingSelf()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1");
			FamilyTestData.SeedUser(db, "owner-1", "Owner", "owner@example.com");
		});

		var handler = new InviteToFamilyCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new InviteToFamilyCommand("owner-1", "owner@example.com"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenInviteeAlreadyMember()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1");
			FamilyTestData.SeedUser(db, "member-1", "Member", "member@example.com");
		});

		var handler = new InviteToFamilyCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new InviteToFamilyCommand("owner-1", "member@example.com"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenInvitationAlreadyPending()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1");
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

		var handler = new InviteToFamilyCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new InviteToFamilyCommand("owner-1", "invitee@example.com"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenEmailMissing()
	{
		var context = TestDbContextFactory.CreateContext();
		var handler = new InviteToFamilyCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new InviteToFamilyCommand("owner-1", " "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}
}
