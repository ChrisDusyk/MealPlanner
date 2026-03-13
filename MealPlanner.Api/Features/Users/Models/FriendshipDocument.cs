using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MealPlanner.Api.Features.Users.Models;

/// <summary>
/// MongoDB persistence document for an accepted friendship pair.
/// </summary>
public class FriendshipDocument
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	public string UserAId { get; set; } = string.Empty;

	public string UserBId { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; }
}
