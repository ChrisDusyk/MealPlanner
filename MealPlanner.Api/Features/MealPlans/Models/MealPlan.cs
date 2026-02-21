using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.MealPlans.Models;

/// <summary>
/// The four meal categories available per day.
/// </summary>
public enum MealCategory
{
	Breakfast,
	Lunch,
	Supper,
	Snacks
}

/// <summary>
/// A single item within a meal slot — either a recipe reference or a free-text entry.
/// When RecipeId is Some, the item links to a recipe in the user's collection.
/// Name is always present (recipe name for references, arbitrary text for free entries).
/// </summary>
public record MealSlotItem(
	Option<string> RecipeId,
	string Name
);

/// <summary>
/// One day's plan, containing a set of meal slots keyed by category.
/// </summary>
public record DayPlan(
	DayOfWeek Day,
	Dictionary<MealCategory, List<MealSlotItem>> Slots
);

/// <summary>
/// A weekly meal plan for a single user, anchored to a Monday start date.
/// </summary>
public record MealPlan(
	string Id,
	string UserId,
	DateOnly WeekStart,
	List<DayPlan> Days,
	DateTime CreatedAt,
	DateTime UpdatedAt
);
