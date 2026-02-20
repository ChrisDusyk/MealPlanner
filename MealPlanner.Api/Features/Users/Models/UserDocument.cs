using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MealPlanner.Api.Features.Users.Models;

/// <summary>
/// MongoDB persistence document for application users.
/// </summary>
public class UserDocument
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	public string Auth0UserId { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string? Email { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}
