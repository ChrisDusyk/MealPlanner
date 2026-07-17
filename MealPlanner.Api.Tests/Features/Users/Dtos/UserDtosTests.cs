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



}
