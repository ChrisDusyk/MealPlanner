namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for an accepted friendship pair.
/// </summary>
public class FriendshipEntity
{
	public Guid Id { get; set; }

	public string UserAId { get; set; } = string.Empty;

	public string UserBId { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; }
}
