namespace MealPlanner.Api.Features.Users.Dtos;

/// <summary>
/// Request payload for sending a friend request by email.
/// </summary>
public record SendFriendRequestRequest(string Email);
