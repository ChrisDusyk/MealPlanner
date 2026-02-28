using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.MealPlans.Dtos;

/// <summary>
/// DTO for a single item within a meal slot.
/// </summary>
public record MealSlotItemDto(
	string? RecipeId,
	string Name,
	int Servings = 1
)
{
	public MealSlotItem ToDomain() =>
		new(Option<string>.From(RecipeId), Name, Servings);

	public static MealSlotItemDto FromDomain(MealSlotItem item) =>
		new(item.RecipeId.GetValueOrNull(), item.Name, item.Servings);
}

/// <summary>
/// DTO for a single day's plan.
/// </summary>
public record DayPlanDto(
	string Day,
	Dictionary<string, List<MealSlotItemDto>> Slots
)
{
	public static DayPlanDto FromDomain(DayPlan dayPlan) =>
		new(
			dayPlan.Day.ToString(),
			dayPlan.Slots.ToDictionary(
				kvp => kvp.Key.ToString(),
				kvp => kvp.Value.Select(MealSlotItemDto.FromDomain).ToList()
			)
		);
}

/// <summary>
/// Response body representing a full weekly meal plan.
/// </summary>
public record MealPlanResponse(
	string Id,
	string WeekStart,
	List<DayPlanDto> Days,
	DateTime CreatedAt,
	DateTime UpdatedAt
)
{
	public static MealPlanResponse FromDomain(MealPlan plan) =>
		new(
			plan.Id,
			plan.WeekStart.ToString("yyyy-MM-dd"),
			plan.Days.Select(DayPlanDto.FromDomain).ToList(),
			plan.CreatedAt,
			plan.UpdatedAt
		);
}

/// <summary>
/// Request body for updating a specific day+category slot.
/// </summary>
public record UpdateDaySlotRequest(
	List<MealSlotItemDto> Items
);

/// <summary>
/// Request body for copying a meal category from a source day to multiple target days.
/// </summary>
public record CopyCategoryRequest(
	string SourceDay,
	string Category,
	List<string> TargetDays
);
