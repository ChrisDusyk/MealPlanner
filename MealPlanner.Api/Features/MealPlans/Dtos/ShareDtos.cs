using MealPlanner.Api.Features.MealPlans.Models;

namespace MealPlanner.Api.Features.MealPlans.Dtos;

/// <summary>
/// Request body for sharing a meal plan with another user.
/// </summary>
public record ShareMealPlanRequest(
	string Email,
	string WeekStart,
	string Permission
);

/// <summary>
/// Response body representing a single share record.
/// </summary>
public record MealPlanShareResponse(
	string Id,
	string OwnerUserId,
	string SharedWithUserId,
	string SharedWithName,
	string SharedWithEmail,
	string WeekStart,
	string Permission,
	DateTime SharedAt
)
{
	public static MealPlanShareResponse FromDomain(
		MealPlanShare share,
		string sharedWithName = "",
		string sharedWithEmail = "") =>
		new(
			Id: share.Id,
			OwnerUserId: share.OwnerUserId,
			SharedWithUserId: share.SharedWithUserId,
			SharedWithName: sharedWithName,
			SharedWithEmail: sharedWithEmail,
			WeekStart: share.WeekStart,
			Permission: share.Permission.ToString(),
			SharedAt: share.SharedAt
		);
}

/// <summary>
/// A shared meal plan as seen by the recipient, including full plan data and owner info.
/// </summary>
public record SharedMealPlanResponse(
	string ShareId,
	string OwnerUserId,
	string OwnerName,
	string OwnerEmail,
	string Permission,
	MealPlanResponse MealPlan
);
