using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Models;

public class GroceryListExportLinkDocument
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	public string UserId { get; set; } = string.Empty;

	public string GroceryListId { get; set; } = string.Empty;

	public string WeekStart { get; set; } = string.Empty;

	public string Provider { get; set; } = IntegrationProvider.GoogleKeep.ToString();

	public string ExternalItemId { get; set; } = string.Empty;

	public DateTime LastExportedAtUtc { get; set; }

	public string? LastSyncHash { get; set; }
}
