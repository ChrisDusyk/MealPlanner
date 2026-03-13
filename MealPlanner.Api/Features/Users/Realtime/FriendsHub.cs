using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MealPlanner.Api.Features.Users.Realtime;

[Authorize]
public class FriendsHub : Hub
{
	public const string HubRoute = "/hubs/friends";
	public const string FriendsUpdatedMethod = "friendsUpdated";
}
