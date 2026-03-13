using MealPlanner.Api.Features.Users.Dtos;
using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Features.Users.Queries;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Users.Dtos;

public class UserDtosTests
{
	[Fact]
	public void UserResponse_FromDomain_MapsAllFields_WhenEmailPresent()
	{
		var user = new User(
			"u1",
			"auth0|123",
			"Pat",
			Option<string>.Some("pat@example.com"),
			new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

		var response = UserResponse.FromDomain(user);

		Assert.Equal("u1", response.Id);
		Assert.Equal("auth0|123", response.Auth0UserId);
		Assert.Equal("Pat", response.Name);
		Assert.Equal("pat@example.com", response.Email);
	}

	[Fact]
	public void UserResponse_FromDomain_MapsNullEmail_WhenMissing()
	{
		var user = new User(
			"u1",
			"auth0|123",
			"Pat",
			Option<string>.None(),
			new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

		var response = UserResponse.FromDomain(user);
		Assert.Null(response.Email);
	}

	[Fact]
	public void UserSummaryResponse_FromDomain_MapsExpectedFields()
	{
		var user = new User(
			"u1",
			"auth0|123",
			"Pat",
			Option<string>.Some("pat@example.com"),
			new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

		var response = UserSummaryResponse.FromDomain(user);

		Assert.Equal("u1", response.Id);
		Assert.Equal("Pat", response.Name);
		Assert.Equal("pat@example.com", response.Email);
	}

	[Fact]
	public void FriendSummaryResponse_FromDomain_MapsExpectedFields()
	{
		var summary = new FriendSummary("auth0|friend", "Friend User", "friend@example.com");

		var response = FriendSummaryResponse.FromDomain(summary);

		Assert.Equal("auth0|friend", response.UserId);
		Assert.Equal("Friend User", response.Name);
		Assert.Equal("friend@example.com", response.Email);
	}

	[Fact]
	public void FriendRequestSummaryResponse_FromDomain_MapsExpectedFields()
	{
		var createdAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
		var summary = new FriendRequestSummary("req-1", "auth0|friend", "Friend User", "friend@example.com", createdAt);

		var response = FriendRequestSummaryResponse.FromDomain(summary);

		Assert.Equal("req-1", response.RequestId);
		Assert.Equal("auth0|friend", response.UserId);
		Assert.Equal(createdAt, response.CreatedAt);
	}

	[Fact]
	public void SendFriendRequestResponse_FromDomain_MapsStatus()
	{
		var response = SendFriendRequestResponse.FromDomain(new SendFriendRequestResult(SendFriendRequestStatus.Accepted));

		Assert.Equal("Accepted", response.Status);
	}
}
