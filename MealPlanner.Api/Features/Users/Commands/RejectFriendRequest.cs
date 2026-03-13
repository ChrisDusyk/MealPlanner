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
) : ICommand<FriendRequestActionResult>;

public class RejectFriendRequestCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<RejectFriendRequestCommand, FriendRequestActionResult>
{
	public async Task<Result<FriendRequestActionResult>> HandleAsync(
		RejectFriendRequestCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.RecipientUserId))
			return Result<FriendRequestActionResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipient user ID is required."));

		if (string.IsNullOrWhiteSpace(command.RequestId))
			return Result<FriendRequestActionResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Friend request ID is required."));

		try
		{
			var requests = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<FriendRequestDocument>("friend_requests");

			var request = await requests
				.Find(r => r.Id == command.RequestId && r.RecipientUserId == command.RecipientUserId)
				.FirstOrDefaultAsync(cancellationToken);

			if (request is null)
			{
				return Result<FriendRequestActionResult>.Failure(
					new Error(ErrorCodes.NotFound, "Friend request was not found."));
			}

			var deleted = await requests.DeleteOneAsync(
				r => r.Id == command.RequestId && r.RecipientUserId == command.RecipientUserId,
				cancellationToken);

			if (deleted.DeletedCount == 0)
			{
				return Result<FriendRequestActionResult>.Failure(
					new Error(ErrorCodes.NotFound, "Friend request was not found."));
			}

			return Result<FriendRequestActionResult>.Success(new FriendRequestActionResult(request.RequesterUserId));
		}
		catch (Exception ex)
		{
			return Result<FriendRequestActionResult>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to reject friend request.", ex));
		}
	}
}
