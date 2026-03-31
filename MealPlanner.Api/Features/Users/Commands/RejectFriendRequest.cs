using MealPlanner.Api.Data;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Users.Commands;

/// <summary>
/// Command to reject an incoming friend request.
/// </summary>
public record RejectFriendRequestCommand(
	string RecipientUserId,
	string RequestId
) : ICommand<FriendRequestActionResult>;

public class RejectFriendRequestCommandHandler(MealPlannerDbContext db)
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

		if (!Guid.TryParse(command.RequestId, out var requestGuid))
			return Result<FriendRequestActionResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Friend request ID is invalid."));

		try
		{
			var request = await db.FriendRequests
				.FirstOrDefaultAsync(r => r.Id == requestGuid && r.RecipientUserId == command.RecipientUserId, cancellationToken);

			if (request is null)
			{
				return Result<FriendRequestActionResult>.Failure(
					new Error(ErrorCodes.NotFound, "Friend request was not found."));
			}

			db.FriendRequests.Remove(request);
			await db.SaveChangesAsync(cancellationToken);

			return Result<FriendRequestActionResult>.Success(new FriendRequestActionResult(request.RequesterUserId));
		}
		catch (Exception ex)
		{
			return Result<FriendRequestActionResult>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to reject friend request.", ex));
		}
	}
}
