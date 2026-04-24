using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.Users.Queries;

public class GetIncomingFriendRequestsTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenRecipientMissing()
	{
		var handler = new GetIncomingFriendRequestsQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetIncomingFriendRequestsQuery(" "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsIncomingRequestSummaries()
	{
		var createdAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.FriendRequests.Add(new FriendRequestEntity
			{
				Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
				RequesterUserId = "auth0|you",
				RecipientUserId = "auth0|me",
				CreatedAt = createdAt
			});

			db.Users.Add(new UserEntity
			{
				Id = Guid.NewGuid(),
				AuthUserId = "auth0|you",
				Name = "You",
				Email = "you@example.com",
				CreatedAt = createdAt,
				UpdatedAt = createdAt
			});
		});

		var handler = new GetIncomingFriendRequestsQueryHandler(context);
		var result = await handler.HandleAsync(new GetIncomingFriendRequestsQuery("auth0|me"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(result.Value!);
		Assert.Equal("auth0|you", result.Value![0].UserId);
	}
}
