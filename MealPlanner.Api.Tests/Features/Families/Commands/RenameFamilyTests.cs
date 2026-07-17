using MealPlanner.Api.Features.Families;
using MealPlanner.Api.Features.Families.Commands;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Families.Commands;

public class RenameFamilyTests
{
	[Fact]
	public async Task HandleAsync_RenamesFamily_WhenCallerIsOwner()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1");
		});

		var handler = new RenameFamilyCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new RenameFamilyCommand("owner-1", "  The Smiths  "), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal("The Smiths", result.Value!.Family.Name);
		Assert.Equal("The Smiths", context.FamilyGroups.Single().Name);
	}

	[Fact]
	public async Task HandleAsync_ReturnsUnauthorized_WhenCallerIsNotOwner()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1");
		});

		var handler = new RenameFamilyCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new RenameFamilyCommand("member-1", "New Name"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.Unauthorized, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenNameEmpty()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1");
		});

		var handler = new RenameFamilyCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new RenameFamilyCommand("owner-1", "   "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenNameTooLong()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1");
		});

		var handler = new RenameFamilyCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new RenameFamilyCommand("owner-1", new string('x', 101)), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}
}
