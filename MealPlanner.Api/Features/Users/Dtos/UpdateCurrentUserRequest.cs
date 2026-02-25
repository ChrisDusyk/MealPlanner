namespace MealPlanner.Api.Features.Users.Dtos;

/// <summary>
/// Request payload for updating the current authenticated user profile.
/// </summary>
public class UpdateCurrentUserRequest
{
	public string? Name { get; set; }
}
