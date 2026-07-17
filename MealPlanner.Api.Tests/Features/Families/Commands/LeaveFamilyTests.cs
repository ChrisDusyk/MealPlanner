using MealPlanner.Api.Features.Families.Commands;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Families.Commands;

public class LeaveFamilyTests
{
	[Fact]
	public async Task HandleAsync_MovesMemberToFreshPersonalFamily_WithRecipeCopies()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1");
			FamilyTestData.SeedUser(db, "member-1", "Member");
			FamilyTestData.SeedRecipe(db, TestIds.Family("fam"), "member-1", "Chili");
			FamilyTestData.SeedRecipe(db, TestIds.Family("fam"), "owner-1", "Stew");
		});

		var handler = new LeaveFamilyCommandHandler(context);
		var result = await handler.HandleAsync(new LeaveFamilyCommand("member-1"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.True(result.Value!.IsOwner);
		Assert.Equal("Member's Family", result.Value.Family.Name);

		var newFamilyId = Guid.Parse(result.Value.Family.Id);
		// Copies of the member's contributed recipes only.
		Assert.Single(context.Recipes.Where(r => r.FamilyGroupId == newFamilyId && r.Name == "Chili"));
		Assert.Empty(context.Recipes.Where(r => r.FamilyGroupId == newFamilyId && r.Name == "Stew"));
		// The family keeps the originals.
		Assert.Single(context.Recipes.Where(r => r.FamilyGroupId == TestIds.Family("fam") && r.Name == "Chili"));
		// Old membership removed.
		Assert.DoesNotContain(context.FamilyGroupMembers,
			m => m.FamilyGroupId == TestIds.Family("fam") && m.UserId == "member-1");
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenOwnerWithOtherMembers()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1");
		});

		var handler = new LeaveFamilyCommandHandler(context);
		var result = await handler.HandleAsync(new LeaveFamilyCommand("owner-1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenSoleMember()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1");
		});

		var handler = new LeaveFamilyCommandHandler(context);
		var result = await handler.HandleAsync(new LeaveFamilyCommand("owner-1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenNotAMember()
	{
		var handler = new LeaveFamilyCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new LeaveFamilyCommand("nobody"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}
}
