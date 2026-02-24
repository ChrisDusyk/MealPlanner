using MealPlanner.Api.Features.Users.Dtos;
using MealPlanner.Api.Features.Users.Models;
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
}
