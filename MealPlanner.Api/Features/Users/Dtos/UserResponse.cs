using MealPlanner.Api.Features.Users.Models;

namespace MealPlanner.Api.Features.Users.Dtos;

/// <summary>
/// API response payload for user data.
/// </summary>
public record UserResponse(
	string Id,
	string Auth0UserId,
	string Name,
	string? Email,
	DateTime CreatedAt,
	DateTime UpdatedAt)
{
	public static UserResponse FromDomain(User user) =>
		new(
			Id: user.Id,
			Auth0UserId: user.Auth0UserId,
			Name: user.Name,
			Email: user.Email.GetValueOrNull(),
			CreatedAt: user.CreatedAt,
			UpdatedAt: user.UpdatedAt
		);
}
