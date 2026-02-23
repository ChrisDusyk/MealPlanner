using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MealPlanner.Api.Features.MealPlans.Models;

/// <summary>
/// MongoDB persistence document for a meal plan share.
/// </summary>
public class MealPlanShareDocument
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	public string OwnerUserId { get; set; } = string.Empty;

	public string SharedWithUserId { get; set; } = string.Empty;

	/// <summary>
	/// The Monday that starts the shared plan week, stored as "yyyy-MM-dd".
	/// </summary>
	public string WeekStart { get; set; } = string.Empty;

	/// <summary>
	/// Permission level: "ReadOnly" or "ReadWrite".
	/// </summary>
	public string Permission { get; set; } = nameof(SharePermission.ReadOnly);

	public DateTime SharedAt { get; set; }

	public bool DismissedByRecipient { get; set; }

	// ── Mapping helpers ──

	public MealPlanShare ToDomain() =>
		new(
			Id: Id ?? string.Empty,
			OwnerUserId: OwnerUserId,
			SharedWithUserId: SharedWithUserId,
			WeekStart: WeekStart,
			Permission: Enum.Parse<SharePermission>(Permission),
			SharedAt: SharedAt,
			DismissedByRecipient: DismissedByRecipient
		);

	public static MealPlanShareDocument FromDomain(MealPlanShare share) =>
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
