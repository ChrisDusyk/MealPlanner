namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for application users.
/// </summary>
public class UserEntity
{
	public Guid Id { get; set; }

	public string Auth0UserId { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string? Email { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}
