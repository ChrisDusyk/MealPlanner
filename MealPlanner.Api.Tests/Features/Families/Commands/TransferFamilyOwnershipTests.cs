using MealPlanner.Api.Features.Families;
using MealPlanner.Api.Features.Families.Commands;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Families.Commands;

public class TransferFamilyOwnershipTests
{
	[Fact]
	public async Task HandleAsync_TransfersOwnership_WhenTargetIsMember()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1");
		});

		var handler = new TransferFamilyOwnershipCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new TransferFamilyOwnershipCommand("owner-1", "member-1"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.False(result.Value!.IsOwner);
		Assert.Equal("member-1", context.FamilyGroups.Single().OwnerUserId);
	}

	[Fact]
	public async Task HandleAsync_ReturnsUnauthorized_WhenCallerIsNotOwner()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1", "member-1", "member-2");
		});

		var handler = new TransferFamilyOwnershipCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new TransferFamilyOwnershipCommand("member-1", "member-2"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.Unauthorized, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenTargetNotAMember()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1");
		});

		var handler = new TransferFamilyOwnershipCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new TransferFamilyOwnershipCommand("owner-1", "stranger"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenTransferringToSelf()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			FamilyTestData.SeedFamily(db, "fam", "owner-1");
		});

		var handler = new TransferFamilyOwnershipCommandHandler(context, new FamilyContextResolver(context));
		var result = await handler.HandleAsync(
			new TransferFamilyOwnershipCommand("owner-1", "owner-1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}
}
