using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Users.Commands;

public class RemoveFriendTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenFriendMissing()
	{
		var handler = new RemoveFriendCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new RemoveFriendCommand("auth0|me", " "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenFriendshipMissing()
	{
		using var db = TestDbContextFactory.CreateContext();
		var handler = new RemoveFriendCommandHandler(db);
		var result = await handler.HandleAsync(new RemoveFriendCommand("auth0|me", "auth0|you"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsSuccess_WhenFriendshipRemoved()
	{
		using var db = TestDbContextFactory.CreateContext();
		db.Friendships.Add(new FriendshipEntity
		{
			Id = Guid.NewGuid(),
			UserAId = "auth0|me",
			UserBId = "auth0|you",
			CreatedAt = DateTime.UtcNow
		});
		await db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var handler = new RemoveFriendCommandHandler(db);
		var result = await handler.HandleAsync(new RemoveFriendCommand("auth0|me", "auth0|you"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Empty(db.Friendships);
	}
}
