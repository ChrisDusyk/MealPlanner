using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MealPlanner.Api.Features.Users.Models;

/// <summary>
/// MongoDB persistence document for a pending friend request.
/// </summary>
public class FriendRequestDocument
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	public string RequesterUserId { get; set; } = string.Empty;

	public string RecipientUserId { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; }
}
