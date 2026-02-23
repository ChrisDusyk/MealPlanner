using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.MealPlans.Models;

/// <summary>
/// Domain record representing a meal plan share between two users for a specific week.
/// </summary>
public record MealPlanShare(
	string Id,
	string OwnerUserId,
	string SharedWithUserId,
	string WeekStart,
	SharePermission Permission,
	DateTime SharedAt,
	bool DismissedByRecipient
);
