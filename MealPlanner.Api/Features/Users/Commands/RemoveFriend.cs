using MealPlanner.Api.Data;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Users.Commands;

/// <summary>
/// Command to remove an existing friend relationship.
/// </summary>
public record RemoveFriendCommand(
	string CurrentUserId,
	string FriendUserId
) : ICommand<Unit>;

public class RemoveFriendCommandHandler(MealPlannerDbContext db)
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
			var (userAId, userBId) = SendFriendRequestByEmailCommandHandler.NormalizePair(
				command.CurrentUserId,
				command.FriendUserId);

			var friendship = await db.Friendships
				.FirstOrDefaultAsync(f => f.UserAId == userAId && f.UserBId == userBId, cancellationToken);

			if (friendship is null)
			{
				return Result<Unit>.Failure(
					new Error(ErrorCodes.NotFound, "Friend relationship was not found."));
			}

			db.Friendships.Remove(friendship);
			await db.SaveChangesAsync(cancellationToken);

			return Result<Unit>.Success(Unit.Value);
		}
		catch (Exception ex)
		{
			return Result<Unit>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to remove friend.", ex));
		}
	}
}
