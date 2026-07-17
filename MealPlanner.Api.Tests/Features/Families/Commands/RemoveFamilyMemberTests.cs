using MealPlanner.Api.Features.Families;
using MealPlanner.Api.Features.Families.Commands;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Families.Commands;

public class RemoveFamilyMemberTests
{
	[Fact]
	public async Task HandleAsync_RemovesMember_AndProvisionsPersonalFamilyWithRecipeCopies()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1");
			FamilyTestData.SeedUser(db, "member-1", "Member");
			FamilyTestData.SeedRecipe(db, TestIds.Family("fam"), "member-1", "Chili");
		});

		var handler = new RemoveFamilyMemberCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new RemoveFamilyMemberCommand("owner-1", "member-1"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!.Family.Members);

		var newMembership = context.FamilyGroupMembers.Single(m => m.UserId == "member-1");
		Assert.NotEqual(TestIds.Family("fam"), newMembership.FamilyGroupId);
		Assert.Single(context.Recipes.Where(r => r.FamilyGroupId == newMembership.FamilyGroupId && r.Name == "Chili"));
		Assert.Single(context.Recipes.Where(r => r.FamilyGroupId == TestIds.Family("fam") && r.Name == "Chili"));
	}

	[Fact]
	public async Task HandleAsync_ReturnsUnauthorized_WhenCallerIsNotOwner()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1", "member-2");
		});

		var handler = new RemoveFamilyMemberCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new RemoveFamilyMemberCommand("member-1", "member-2"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.Unauthorized, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenRemovingSelf()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1");
		});

		var handler = new RemoveFamilyMemberCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new RemoveFamilyMemberCommand("owner-1", "owner-1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenTargetNotAMember()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1");
		});

		var handler = new RemoveFamilyMemberCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new RemoveFamilyMemberCommand("owner-1", "stranger"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}
}
