namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for a weekly meal plan.
/// </summary>
public class MealPlanEntity
{
	public Guid Id { get; set; }

	public string UserId { get; set; } = string.Empty;

	/// <summary>
	/// The Monday that starts this plan week, stored as "yyyy-MM-dd".
	/// </summary>
	public string WeekStart { get; set; } = string.Empty;

	/// <summary>
	/// Stored as jsonb in PostgreSQL.
	/// </summary>
	public List<DayPlanData> Days { get; set; } = [];

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Embedded day plan data stored as part of the meal plan jsonb column.
/// </summary>
public class DayPlanData
{
	public string Day { get; set; } = string.Empty;

	public Dictionary<string, List<MealSlotItemData>> Slots { get; set; } = new();
}

/// <summary>
/// Embedded meal slot item data stored as part of the day plan jsonb.
/// </summary>
public class MealSlotItemData
{
	public string? RecipeId { get; set; }

	public string Name { get; set; } = string.Empty;

	public int Servings { get; set; } = 1;
}
