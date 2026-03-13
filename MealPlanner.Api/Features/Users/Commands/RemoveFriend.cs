using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Users.Commands;

/// <summary>
/// Command to remove an existing friend relationship.
/// </summary>
public record RemoveFriendCommand(
	string CurrentUserId,
	string FriendUserId
) : ICommand<Unit>;

public class RemoveFriendCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<RemoveFriendCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		RemoveFriendCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.CurrentUserId))
			return Result<Unit>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Current user ID is required."));

		if (string.IsNullOrWhiteSpace(command.FriendUserId))
			return Result<Unit>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Friend user ID is required."));

		if (command.CurrentUserId == command.FriendUserId)
			return Result<Unit>.Failure(
				new Error(ErrorCodes.ValidationFailed, "You cannot remove yourself as a friend."));

		try
		{
			var friendships = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<FriendshipDocument>("friendships");

			var (userAId, userBId) = SendFriendRequestByEmailCommandHandler.NormalizePair(
				command.CurrentUserId,
				command.FriendUserId);

			var deleted = await friendships.DeleteOneAsync(
				f => f.UserAId == userAId && f.UserBId == userBId,
				cancellationToken);

			if (deleted.DeletedCount == 0)
			{
				return Result<Unit>.Failure(
					new Error(ErrorCodes.NotFound, "Friend relationship was not found."));
			}

			return Result<Unit>.Success(Unit.Value);
		}
		catch (Exception ex)
		{
			return Result<Unit>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to remove friend.", ex));
		}
	}
}
