using MealPlanner.Api.Features.Users.Queries;

namespace MealPlanner.Api.Features.Users.Dtos;

/// <summary>
/// Response payload for a pending friend request list item.
/// </summary>
public record FriendRequestSummaryResponse(
	string RequestId,
	string UserId,
	string Name,
	string? Email,
	DateTime CreatedAt
)
{
	public static FriendRequestSummaryResponse FromDomain(FriendRequestSummary item) =>
		new(
			RequestId: item.RequestId,
			UserId: item.UserId,
			Name: item.Name,
			Email: item.Email,
			CreatedAt: item.CreatedAt
		);
}
