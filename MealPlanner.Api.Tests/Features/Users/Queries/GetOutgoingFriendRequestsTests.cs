using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.Users.Queries;

public class GetOutgoingFriendRequestsTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenRequesterMissing()
	{
		var handler = new GetOutgoingFriendRequestsQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetOutgoingFriendRequestsQuery(" "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsOutgoingRequestSummaries()
	{
		var createdAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.FriendRequests.Add(new FriendRequestEntity
			{
				Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
				RequesterUserId = "auth0|me",
				RecipientUserId = "auth0|you",
				CreatedAt = createdAt
			});

			db.Users.Add(new UserEntity
			{
				Id = Guid.NewGuid(),
				Auth0UserId = "auth0|you",
				Name = "You",
				Email = "you@example.com",
				CreatedAt = createdAt,
				UpdatedAt = createdAt
			});
		});

		var handler = new GetOutgoingFriendRequestsQueryHandler(context);
		var result = await handler.HandleAsync(new GetOutgoingFriendRequestsQuery("auth0|me"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!);
		Assert.Equal("auth0|you", result.Value![0].UserId);
	}
}
