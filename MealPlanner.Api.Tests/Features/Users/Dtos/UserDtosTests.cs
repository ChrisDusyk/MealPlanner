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
		var completedAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);
		var user = new User(
			"u1",
			"better-auth|123",
			"Pat",
			Option<string>.Some("pat@example.com"),
			Option<string>.Some("Pat H."),
			Option<string>.Some("America/Toronto"),
			Option<DateTime>.Some(completedAt),
			new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

		var response = UserResponse.FromDomain(user);

		Assert.Equal("u1", response.Id);
		Assert.Equal("better-auth|123", response.AuthUserId);
		Assert.Equal("Pat", response.Name);
		Assert.Equal("pat@example.com", response.Email);
		Assert.Equal("Pat H.", response.DisplayName);
		Assert.Equal("America/Toronto", response.Timezone);
		Assert.Equal(completedAt, response.OnboardingCompletedAt);
	}

	[Fact]
	public void UserResponse_FromDomain_MapsNullEmail_WhenMissing()
	{
		var user = new User(
			"u1",
			"better-auth|123",
			"Pat",
			Option<string>.None(),
			Option<string>.None(),
			Option<string>.None(),
			Option<DateTime>.None(),
			new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

		var response = UserResponse.FromDomain(user);
		Assert.Null(response.Email);
		Assert.Null(response.DisplayName);
		Assert.Null(response.Timezone);
		Assert.Null(response.OnboardingCompletedAt);
	}

	[Fact]
	public void UserSummaryResponse_FromDomain_MapsExpectedFields()
	{
		var user = new User(
			"u1",
			"better-auth|123",
			"Pat",
			Option<string>.Some("pat@example.com"),
			Option<string>.None(),
			Option<string>.None(),
			Option<DateTime>.None(),
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
		var summary = new FriendSummary("auth0|friend", "Friend User", "friend@example.com", true, false);

		var response = FriendSummaryResponse.FromDomain(summary);

		Assert.Equal("auth0|friend", response.UserId);
		Assert.Equal("Friend User", response.Name);
		Assert.Equal("friend@example.com", response.Email);
		Assert.True(response.AutoShareMealPlans);
		Assert.False(response.AutoShareGroceryLists);
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
		var response = SendFriendRequestResponse.FromDomain(
			new SendFriendRequestResult(SendFriendRequestStatus.Accepted, "auth0|friend"));

		Assert.Equal("Accepted", response.Status);
	}
}
