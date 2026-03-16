using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MealPlanner.Api.Features.Users.Models;

/// <summary>
/// MongoDB persistence document for per-friend auto-share preferences.
/// </summary>
public class FriendAutoSharePreferenceDocument
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	public string UserId { get; set; } = string.Empty;

	public string FriendUserId { get; set; } = string.Empty;

	public bool AutoShareMealPlans { get; set; }

	public bool AutoShareGroceryLists { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}
