namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for a pending friend request.
/// </summary>
public class FriendRequestEntity
{
	public Guid Id { get; set; }

	public string RequesterUserId { get; set; } = string.Empty;

	public string RecipientUserId { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; }
}
