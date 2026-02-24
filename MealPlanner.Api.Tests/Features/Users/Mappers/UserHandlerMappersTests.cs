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
}
