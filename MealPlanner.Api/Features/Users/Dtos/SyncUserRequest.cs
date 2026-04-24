namespace MealPlanner.Api.Features.Users.Dtos;

/// <summary>
/// Request payload for syncing an authenticated user into the MealPlanner
/// database after they sign in through the identity provider (Better Auth).
/// Fields are optional: any missing values are inferred from JWT claims on
/// the server side.
/// </summary>
public class SyncUserRequest
{
	public string? Name { get; set; }

	public string? Email { get; set; }
}
