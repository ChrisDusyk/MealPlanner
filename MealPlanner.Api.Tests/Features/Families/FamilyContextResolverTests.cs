using MealPlanner.Api.Features.Families;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Families;

public class FamilyContextResolverTests
{
	[Fact]
	public async Task ResolveAsync_ReturnsExistingMembership_WhenUserBelongsToFamily()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1");
		});

		var resolver = new FamilyContextResolver(context);
		var result = await resolver.ResolveAsync("member-1", TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal(TestIds.Family("fam"), result.Value!.FamilyGroupId);
		Assert.False(result.Value.IsOwner);
		Assert.Equal("owner-1", result.Value.OwnerUserId);
	}

	[Fact]
	public async Task ResolveAsync_ReportsOwner_WhenCallerOwnsFamily()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1");
		});

		var resolver = new FamilyContextResolver(context);
		var result = await resolver.ResolveAsync("owner-1", TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.True(result.Value!.IsOwner);
	}

	[Fact]
	public async Task ResolveAsync_ProvisionsPersonalFamily_WhenUserHasNone()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedUser(db, "new-user", "Pat");
		});

		var resolver = new FamilyContextResolver(context);
		var result = await resolver.ResolveAsync("new-user", TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.True(result.Value!.IsOwner);
		var family = context.FamilyGroups.Single();
		Assert.Equal("new-user", family.OwnerUserId);
		Assert.Equal("Pat's Family", family.Name);
		Assert.Single(context.FamilyGroupMembers.Where(m => m.UserId == "new-user"));
	}

	[Fact]
	public async Task ResolveAsync_UsesFallbackFamilyName_WhenUserRowMissing()
	{
		var context = TestDbContextFactory.CreateContext();

		var resolver = new FamilyContextResolver(context);
		var result = await resolver.ResolveAsync("ghost-user", TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal("My Family", context.FamilyGroups.Single().Name);
	}

	[Fact]
	public async Task ResolveAsync_ReturnsValidationFailure_WhenUserIdMissing()
	{
		var resolver = new FamilyContextResolver(TestDbContextFactory.CreateContext());
		var result = await resolver.ResolveAsync(" ", TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task ResolveAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var resolver = new FamilyContextResolver(context);
		var result = await resolver.ResolveAsync("u1", TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
