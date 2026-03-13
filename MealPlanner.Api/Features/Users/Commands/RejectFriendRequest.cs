using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Users.Commands;

/// <summary>
/// Command to reject an incoming friend request.
/// </summary>
public record RejectFriendRequestCommand(
	string RecipientUserId,
	string RequestId
) : ICommand<Unit>;

public class RejectFriendRequestCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<RejectFriendRequestCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		RejectFriendRequestCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.RecipientUserId))
			return Result<Unit>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipient user ID is required."));

		if (string.IsNullOrWhiteSpace(command.RequestId))
			return Result<Unit>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Friend request ID is required."));

		try
		{
			var requests = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<FriendRequestDocument>("friend_requests");

			var deleted = await requests.DeleteOneAsync(
				r => r.Id == command.RequestId && r.RecipientUserId == command.RecipientUserId,
				cancellationToken);

			if (deleted.DeletedCount == 0)
			{
				return Result<Unit>.Failure(
					new Error(ErrorCodes.NotFound, "Friend request was not found."));
			}

			return Result<Unit>.Success(Unit.Value);
		}
		catch (Exception ex)
		{
			return Result<Unit>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to reject friend request.", ex));
		}
	}
}
