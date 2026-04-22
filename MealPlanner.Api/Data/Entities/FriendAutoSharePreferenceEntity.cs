namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for per-friend auto-share preferences.
/// </summary>
public class FriendAutoSharePreferenceEntity
{
	public Guid Id { get; set; }

	public string UserId { get; set; } = string.Empty;

	public string FriendUserId { get; set; } = string.Empty;

	public bool AutoShareMealPlans { get; set; }

	public bool AutoShareGroceryLists { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}
