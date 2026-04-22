using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.Users.Commands;

public class RejectFriendRequestTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenRequestIdMissing()
	{
		var handler = new RejectFriendRequestCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new RejectFriendRequestCommand("auth0|me", " "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenRequestNotFound()
	{
		using var db = TestDbContextFactory.CreateContext();
		var handler = new RejectFriendRequestCommandHandler(db);
		var result = await handler.HandleAsync(new RejectFriendRequestCommand("auth0|me", "11111111-1111-1111-1111-111111111111"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsSuccess_WhenDeleted()
	{
		var request = new FriendRequestEntity
		{
			Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
			RequesterUserId = "auth0|you",
			RecipientUserId = "auth0|me",
			CreatedAt = DateTime.UtcNow
		};

		using var db = TestDbContextFactory.CreateContext();
		db.FriendRequests.Add(request);
		await db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var handler = new RejectFriendRequestCommandHandler(db);
		var result = await handler.HandleAsync(new RejectFriendRequestCommand("auth0|me", "44444444-4444-4444-4444-444444444444"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal("auth0|you", result.Value?.RequesterUserId);
		Assert.Empty(db.FriendRequests);
	}
}
