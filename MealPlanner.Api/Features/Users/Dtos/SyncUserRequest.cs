namespace MealPlanner.Api.Features.Users.Dtos;

/// <summary>
/// Request payload for syncing authenticated users from Auth0.
/// </summary>
public class SyncUserRequest
{
	public string? Name { get; set; }

	public string? Email { get; set; }
}
