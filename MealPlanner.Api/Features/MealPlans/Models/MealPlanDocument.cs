using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MealPlanner.Api.Features.MealPlans.Models;

/// <summary>
/// MongoDB persistence document for a weekly meal plan.
/// </summary>
public class MealPlanDocument
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string? Id { get; set; }

	public string UserId { get; set; } = string.Empty;

	/// <summary>
	/// The Monday that starts this plan week, stored as a plain date string (yyyy-MM-dd).
	/// </summary>
	public string WeekStart { get; set; } = string.Empty;

	public List<DayPlanDocument> Days { get; set; } = [];

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// MongoDB embedded document for a single day's plan.
/// </summary>
public class DayPlanDocument
{
	public string Day { get; set; } = string.Empty;

	public Dictionary<string, List<MealSlotItemDocument>> Slots { get; set; } = new();
}

/// <summary>
/// MongoDB embedded document for a single item within a meal slot.
/// </summary>
public class MealSlotItemDocument
{
	public string? RecipeId { get; set; }

	public string Name { get; set; } = string.Empty;
}
