using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MealPlanner.Api.Features.GroceryLists.Realtime;

[Authorize]
public class GroceryListHub : Hub
{
	public const string HubRoute = "/hubs/grocery-lists";
	public const string GroceryListUpdatedMethod = "groceryListUpdated";
}
