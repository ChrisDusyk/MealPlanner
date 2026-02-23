using MealPlanner.Api.Features.Users.Models;

namespace MealPlanner.Api.Features.Users.Dtos;

/// <summary>
/// Minimal user info returned by search endpoints.
/// </summary>
public record UserSummaryResponse(
	string Id,
	string Name,
	string? Email
)
{
	public static UserSummaryResponse FromDomain(User user) =>
		new(
			Id: user.Id,
			Name: user.Name,
			Email: user.Email.GetValueOrNull()
		);
}
