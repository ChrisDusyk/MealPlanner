using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Families;
using MealPlanner.Api.Features.Families.Commands;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Families.Commands;

public class DeleteFamilyTests
{
	[Fact]
	public async Task HandleAsync_DeletesFamily_AndProvisionsPersonalFamiliesForAllMembers()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1");
			FamilyTestData.SeedUser(db, "owner-1", "Owner");
			FamilyTestData.SeedUser(db, "member-1", "Member");
			FamilyTestData.SeedRecipe(db, TestIds.Family("fam"), "owner-1", "Stew");
			FamilyTestData.SeedRecipe(db, TestIds.Family("fam"), "member-1", "Chili");
			db.MealPlans.Add(new MealPlanEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = TestIds.Family("fam"),
				WeekStart = "2026-02-23",
				Days = [],
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
			db.GroceryLists.Add(new GroceryListEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = TestIds.Family("fam"),
				WeekStart = "2026-02-23",
				Items = [],
				PantryStapleItems = [],
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
		});

		var handler = new DeleteFamilyCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(new DeleteFamilyCommand("owner-1"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.True(result.Value!.IsOwner);
		Assert.Equal("Owner's Family", result.Value.Family.Name);

		// The shared family and its content are gone.
		Assert.DoesNotContain(context.FamilyGroups, f => f.Id == TestIds.Family("fam"));
		Assert.Empty(context.MealPlans);
		Assert.Empty(context.GroceryLists);

		// Both members have fresh personal families with their contributed recipes.
		var ownerMembership = context.FamilyGroupMembers.Single(m => m.UserId == "owner-1");
		var memberMembership = context.FamilyGroupMembers.Single(m => m.UserId == "member-1");
		Assert.NotEqual(ownerMembership.FamilyGroupId, memberMembership.FamilyGroupId);
		Assert.Single(context.Recipes.Where(r => r.FamilyGroupId == ownerMembership.FamilyGroupId && r.Name == "Stew"));
		Assert.Single(context.Recipes.Where(r => r.FamilyGroupId == memberMembership.FamilyGroupId && r.Name == "Chili"));
	}

	[Fact]
	public async Task HandleAsync_ReturnsUnauthorized_WhenCallerIsNotOwner()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1");
		});

		var handler = new DeleteFamilyCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(new DeleteFamilyCommand("member-1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.Unauthorized, result.Error?.Code);
	}
}
