using MealPlanner.Api.Features.Users.Commands;

namespace MealPlanner.Api.Features.Users.Dtos;

/// <summary>
/// Result payload for sending a friend request.
/// </summary>
public record SendFriendRequestResponse(string Status)
{
	public static SendFriendRequestResponse FromDomain(SendFriendRequestResult result) =>
		new(result.Status.ToString());
}
