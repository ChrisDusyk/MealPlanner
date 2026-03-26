using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Models;

public class GoogleIntegrationConnectionDocument
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	public string UserId { get; set; } = string.Empty;

	public string GoogleSubject { get; set; } = string.Empty;

	public string Provider { get; set; } = IntegrationProvider.GoogleKeep.ToString();

	public string? GoogleEmail { get; set; }

	public string EncryptedAccessToken { get; set; } = string.Empty;

	public string? EncryptedRefreshToken { get; set; }

	public DateTime? AccessTokenExpiresAtUtc { get; set; }

	public List<string> Scopes { get; set; } = [];

	public string Capability { get; set; } = IntegrationCapability.Unknown.ToString();

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }

	public DateTime? DisconnectedAtUtc { get; set; }
}
