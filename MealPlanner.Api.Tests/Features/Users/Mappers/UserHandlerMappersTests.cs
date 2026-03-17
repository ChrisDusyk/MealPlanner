using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Features.Users.Queries;

namespace MealPlanner.Api.Tests.Features.Users.Mappers;

public class UserHandlerMappersTests
{
	private static UserDocument CreateDocument(string? email)
	{
		return new UserDocument
		{
			Id = "u1",
			Auth0UserId = "auth0|123",
			Name = "Pat",
			Email = email,
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
		};
	}

	[Fact]
	public void FindUserByEmail_MapToDomain_MapsEmailSome_WhenPresent()
	{
		var user = FindUserByEmailQueryHandler.MapToDomain(CreateDocument("pat@example.com"));

		Assert.Equal("u1", user.Id);
		Assert.True(user.Email.HasValue);
		Assert.Equal("pat@example.com", user.Email.Value);
	}

	[Fact]
	public void UpsertUserFromAuth_MapToDomain_MapsEmailNone_WhenMissing()
	{
		var user = UpsertUserFromAuthCommandHandler.MapToDomain(CreateDocument(null));

		Assert.Equal("u1", user.Id);
		Assert.False(user.Email.HasValue);
	}

	[Fact]
	public void FindUserByAuth0Id_MapToDomain_MapsEmailSome_WhenPresent()
	{
		var user = FindUserByAuth0IdQueryHandler.MapToDomain(CreateDocument("pat@example.com"));

		Assert.Equal("u1", user.Id);
		Assert.True(user.Email.HasValue);
		Assert.Equal("pat@example.com", user.Email.Value);
	}

	[Fact]
	public void UpdateCurrentUserName_MapToDomain_MapsEmailNone_WhenMissing()
	{
		var user = UpdateCurrentUserNameCommandHandler.MapToDomain(CreateDocument(null));

		Assert.Equal("u1", user.Id);
		Assert.False(user.Email.HasValue);
	}

	[Fact]
	public void GetFriendsForUser_MapToSummary_MapsUserDocument()
	{
		var summary = GetFriendsForUserQueryHandler.MapToSummary(CreateDocument("pal@example.com"), null);

		Assert.Equal("auth0|123", summary.UserId);
		Assert.Equal("Pat", summary.Name);
		Assert.Equal("pal@example.com", summary.Email);
		Assert.False(summary.AutoShareMealPlans);
		Assert.False(summary.AutoShareGroceryLists);
	}

	[Fact]
	public void SendFriendRequestByEmail_NormalizePair_OrdersLexicographically()
	{
		var normalized = SendFriendRequestByEmailCommandHandler.NormalizePair("auth0|z", "auth0|a");

		Assert.Equal("auth0|a", normalized.UserAId);
		Assert.Equal("auth0|z", normalized.UserBId);
	}
}
