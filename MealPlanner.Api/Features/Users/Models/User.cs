using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Users.Models;

/// <summary>
/// Domain record representing an application user linked to Auth0.
/// </summary>
public record User(
	string Id,
	string Auth0UserId,
	string Name,
	Option<string> Email,
	DateTime CreatedAt,
	DateTime UpdatedAt
);
