using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Families.Commands;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Families.Commands;

public class AcceptFamilyInvitationTests
{
	private static FamilyInvitationEntity SeedInvitation(
		MealPlanner.Api.Data.MealPlannerDbContext db,
		string familyKey,
		string inviterUserId,
		string inviteeUserId)
	{
		var invitation = new FamilyInvitationEntity
		{
			Id = Guid.NewGuid(),
			FamilyGroupId = TestIds.Family(familyKey),
			InviterUserId = inviterUserId,
			InviteeUserId = inviteeUserId,
			CreatedAt = DateTime.UtcNow
		};
		db.FamilyInvitations.Add(invitation);
		return invitation;
	}

	[Fact]
	public async Task HandleAsync_MovesInviteeIntoFamily_AndDeletesEmptyPersonalFamily()
	{
		Guid invitationId = Guid.Empty;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "target", "owner-1");
			FamilyTestData.SeedFamily(db, "personal", "joiner-1");
			FamilyTestData.SeedRecipe(db, TestIds.Family("personal"), "joiner-1", "Chili");
			db.MealPlans.Add(new MealPlanEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = TestIds.Family("personal"),
				WeekStart = "2026-02-23",
				Days = [],
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
			invitationId = SeedInvitation(db, "target", "owner-1", "joiner-1").Id;
		});

		var handler = new AcceptFamilyInvitationCommandHandler(context);
		var result = await handler.HandleAsync(
			new AcceptFamilyInvitationCommand("joiner-1", invitationId.ToString()),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		// Membership moved to the target family.
		var membership = context.FamilyGroupMembers.Single(m => m.UserId == "joiner-1");
		Assert.Equal(TestIds.Family("target"), membership.FamilyGroupId);
		// The old personal family and its content are gone.
		Assert.DoesNotContain(context.FamilyGroups, f => f.Id == TestIds.Family("personal"));
		Assert.DoesNotContain(context.MealPlans, p => p.FamilyGroupId == TestIds.Family("personal"));
		// Contributed recipe was carried into the target family.
		Assert.Single(context.Recipes.Where(r =>
			r.FamilyGroupId == TestIds.Family("target") && r.ContributedByUserId == "joiner-1" && r.Name == "Chili"));
		// The invitation is consumed.
		Assert.Empty(context.FamilyInvitations);
	}

	[Fact]
	public async Task HandleAsync_KeepsOldFamily_WhenOtherMembersRemain()
	{
		Guid invitationId = Guid.Empty;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "target", "owner-1");
			FamilyTestData.SeedFamily(db, "old", "old-owner", "joiner-1");
			FamilyTestData.SeedRecipe(db, TestIds.Family("old"), "joiner-1", "Chili");
			invitationId = SeedInvitation(db, "target", "owner-1", "joiner-1").Id;
		});

		var handler = new AcceptFamilyInvitationCommandHandler(context);
		var result = await handler.HandleAsync(
			new AcceptFamilyInvitationCommand("joiner-1", invitationId.ToString()),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		// Old family still exists and keeps the original recipe.
		Assert.Contains(context.FamilyGroups, f => f.Id == TestIds.Family("old"));
		Assert.Single(context.Recipes.Where(r => r.FamilyGroupId == TestIds.Family("old") && r.Name == "Chili"));
		// The target family received a copy.
		Assert.Single(context.Recipes.Where(r => r.FamilyGroupId == TestIds.Family("target") && r.Name == "Chili"));
	}

	[Fact]
	public async Task HandleAsync_SkipsCatalogueDuplicates_WhenTargetAlreadyHasRecipe()
	{
		var catalogueId = Guid.NewGuid();
		Guid invitationId = Guid.Empty;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "target", "owner-1");
			FamilyTestData.SeedRecipe(db, TestIds.Family("target"), "owner-1", "Catalogue Dish", catalogueId);
			FamilyTestData.SeedFamily(db, "personal", "joiner-1");
			FamilyTestData.SeedRecipe(db, TestIds.Family("personal"), "joiner-1", "Catalogue Dish", catalogueId);
			invitationId = SeedInvitation(db, "target", "owner-1", "joiner-1").Id;
		});

		var handler = new AcceptFamilyInvitationCommandHandler(context);
		var result = await handler.HandleAsync(
			new AcceptFamilyInvitationCommand("joiner-1", invitationId.ToString()),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(context.Recipes.Where(r =>
			r.FamilyGroupId == TestIds.Family("target") && r.CatalogueRecipeId == catalogueId));
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenOwnerOfMultiMemberFamily()
	{
		Guid invitationId = Guid.Empty;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "target", "other-owner");
			FamilyTestData.SeedFamily(db, "current", "joiner-1", "member-2");
			invitationId = SeedInvitation(db, "target", "other-owner", "joiner-1").Id;
		});

		var handler = new AcceptFamilyInvitationCommandHandler(context);
		var result = await handler.HandleAsync(
			new AcceptFamilyInvitationCommand("joiner-1", invitationId.ToString()),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
		// Membership unchanged.
		Assert.Equal(TestIds.Family("current"),
			context.FamilyGroupMembers.Single(m => m.UserId == "joiner-1").FamilyGroupId);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenInvitationIsForAnotherUser()
	{
		Guid invitationId = Guid.Empty;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "target", "owner-1");
			invitationId = SeedInvitation(db, "target", "owner-1", "someone-else").Id;
		});

		var handler = new AcceptFamilyInvitationCommandHandler(context);
		var result = await handler.HandleAsync(
			new AcceptFamilyInvitationCommand("joiner-1", invitationId.ToString()),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenInvitationIdInvalid()
	{
		var handler = new AcceptFamilyInvitationCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new AcceptFamilyInvitationCommand("joiner-1", "not-a-guid"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}
}
