using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Users.Commands;

public enum SendFriendRequestStatus
{
	Pending,
	Accepted
}

public record SendFriendRequestResult(
	SendFriendRequestStatus Status,
	string RecipientUserId);

/// <summary>
/// Command to send a friend request to another user by email.
/// </summary>
public record SendFriendRequestByEmailCommand(
	string RequesterUserId,
	string RecipientEmail
) : ICommand<SendFriendRequestResult>;

/// <summary>
/// Handles sending friend requests with reciprocal-request auto-accept behavior.
/// </summary>
public class SendFriendRequestByEmailCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<SendFriendRequestByEmailCommand, SendFriendRequestResult>
{
	public async Task<Result<SendFriendRequestResult>> HandleAsync(
		SendFriendRequestByEmailCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.RequesterUserId))
			return Result<SendFriendRequestResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Requester user ID is required."));

		if (string.IsNullOrWhiteSpace(command.RecipientEmail))
			return Result<SendFriendRequestResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipient email is required."));

		try
		{
			var requester = await db.Users
				.FirstOrDefaultAsync(u => u.AuthUserId == command.RequesterUserId, cancellationToken);
			if (requester is null)
			{
				return Result<SendFriendRequestResult>.Failure(
					new Error(ErrorCodes.NotFound, "Current user was not found."));
			}

			var normalizedEmail = command.RecipientEmail.Trim().ToUpperInvariant();
			var recipient = await db.Users
				.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToUpper() == normalizedEmail, cancellationToken);

			if (recipient is null)
			{
				return Result<SendFriendRequestResult>.Failure(
					new Error(ErrorCodes.ValidationFailed, "No user exists with that email address."));
			}

			if (recipient.AuthUserId == command.RequesterUserId)
			{
				return Result<SendFriendRequestResult>.Failure(
					new Error(ErrorCodes.ValidationFailed, "You cannot send a friend request to yourself."));
			}

			var (userAId, userBId) = NormalizePair(command.RequesterUserId, recipient.AuthUserId);
			var existingFriendship = await db.Friendships
				.AnyAsync(f => f.UserAId == userAId && f.UserBId == userBId, cancellationToken);
			if (existingFriendship)
			{
				return Result<SendFriendRequestResult>.Failure(
					new Error(ErrorCodes.ValidationFailed, "You are already friends with this user."));
			}

			var existingRequest = await db.FriendRequests
				.AnyAsync(r => r.RequesterUserId == command.RequesterUserId && r.RecipientUserId == recipient.AuthUserId, cancellationToken);
			if (existingRequest)
			{
				return Result<SendFriendRequestResult>.Failure(
					new Error(ErrorCodes.ValidationFailed, "A friend request is already pending for this user."));
			}

			var reciprocalRequest = await db.FriendRequests
				.FirstOrDefaultAsync(r => r.RequesterUserId == recipient.AuthUserId && r.RecipientUserId == command.RequesterUserId, cancellationToken);

			if (reciprocalRequest is not null)
			{
				var now = DateTime.UtcNow;
				var existingPair = await db.Friendships
					.AnyAsync(f => f.UserAId == userAId && f.UserBId == userBId, cancellationToken);
				if (!existingPair)
				{
					db.Friendships.Add(new FriendshipEntity
					{
						Id = Guid.NewGuid(),
						UserAId = userAId,
						UserBId = userBId,
						CreatedAt = now
					});
				}

				db.FriendRequests.Remove(reciprocalRequest);
				await db.SaveChangesAsync(cancellationToken);

				return Result<SendFriendRequestResult>.Success(
					new SendFriendRequestResult(
						SendFriendRequestStatus.Accepted,
						recipient.AuthUserId));
			}

			db.FriendRequests.Add(new FriendRequestEntity
			{
				Id = Guid.NewGuid(),
				RequesterUserId = command.RequesterUserId,
				RecipientUserId = recipient.AuthUserId,
				CreatedAt = DateTime.UtcNow
			});
			await db.SaveChangesAsync(cancellationToken);

			return Result<SendFriendRequestResult>.Success(
				new SendFriendRequestResult(
					SendFriendRequestStatus.Pending,
					recipient.AuthUserId));
		}
		catch (DbUpdateException)
		{
			return Result<SendFriendRequestResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "A friend request is already pending for this user."));
		}
		catch (Exception ex)
		{
			return Result<SendFriendRequestResult>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to send friend request.", ex));
		}
	}

	internal static (string UserAId, string UserBId) NormalizePair(string leftUserId, string rightUserId)
	{
		return string.CompareOrdinal(leftUserId, rightUserId) <= 0
			? (leftUserId, rightUserId)
			: (rightUserId, leftUserId);
	}
}
