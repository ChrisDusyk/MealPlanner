namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for a meal plan share.
/// </summary>
public class MealPlanShareEntity
{
	public Guid Id { get; set; }

	public string OwnerUserId { get; set; } = string.Empty;

	public string SharedWithUserId { get; set; } = string.Empty;

	/// <summary>
	/// The Monday that starts the shared plan week, stored as "yyyy-MM-dd".
	/// </summary>
	public string WeekStart { get; set; } = string.Empty;

	/// <summary>
	/// Permission level: "ReadOnly" or "ReadWrite".
	/// </summary>
	public string Permission { get; set; } = "ReadOnly";

	public DateTime SharedAt { get; set; }

	public bool DismissedByRecipient { get; set; }
}
