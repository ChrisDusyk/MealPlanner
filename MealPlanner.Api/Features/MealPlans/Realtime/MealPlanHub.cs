using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MealPlanner.Api.Features.MealPlans.Realtime;

[Authorize]
public class MealPlanHub : Hub
{
	public const string HubRoute = "/hubs/meal-plans";
	public const string MealPlanUpdatedMethod = "mealPlanUpdated";
}
