using MealPlanner.Api.Features.MealPlans.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MealPlanner.Api.Features.GroceryLists.Models;

/// <summary>
/// MongoDB persistence document for a grocery list share.
/// </summary>
public class GroceryListShareDocument
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	public string OwnerUserId { get; set; } = string.Empty;

	public string SharedWithUserId { get; set; } = string.Empty;

	/// <summary>
	/// The Monday that starts the shared grocery list week, stored as "yyyy-MM-dd".
	/// </summary>
	public string WeekStart { get; set; } = string.Empty;

	/// <summary>
	/// Permission level: "ReadOnly" or "ReadWrite".
	/// </summary>
	public string Permission { get; set; } = nameof(SharePermission.ReadOnly);

	public DateTime SharedAt { get; set; }

	public bool DismissedByRecipient { get; set; }

	// ── Mapping helpers ──

	public GroceryListShare ToDomain() =>
		new(
			Id: Id ?? string.Empty,
			OwnerUserId: OwnerUserId,
			SharedWithUserId: SharedWithUserId,
			WeekStart: WeekStart,
			Permission: Enum.Parse<SharePermission>(Permission),
			SharedAt: SharedAt,
			DismissedByRecipient: DismissedByRecipient
		);

	public static GroceryListShareDocument FromDomain(GroceryListShare share) =>
		new()
		{
			Id = string.IsNullOrEmpty(share.Id) ? null : share.Id,
			OwnerUserId = share.OwnerUserId,
			SharedWithUserId = share.SharedWithUserId,
			WeekStart = share.WeekStart,
			Permission = share.Permission.ToString(),
			SharedAt = share.SharedAt,
			DismissedByRecipient = share.DismissedByRecipient
		};
}
