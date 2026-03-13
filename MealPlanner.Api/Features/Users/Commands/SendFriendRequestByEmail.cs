using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

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
public class SendFriendRequestByEmailCommandHandler(IMongoClient mongoClient)
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
			var database = mongoClient.GetDatabase("mealplannerDb");
			var users = database.GetCollection<UserDocument>("users");
			var friendships = database.GetCollection<FriendshipDocument>("friendships");
			var requests = database.GetCollection<FriendRequestDocument>("friend_requests");

			var requester = await users
				.Find(u => u.Auth0UserId == command.RequesterUserId)
				.FirstOrDefaultAsync(cancellationToken);
			if (requester is null)
			{
				return Result<SendFriendRequestResult>.Failure(
					new Error(ErrorCodes.NotFound, "Current user was not found."));
			}

			var emailPattern = new BsonRegularExpression($"^{Regex.Escape(command.RecipientEmail.Trim())}$", "i");
			var recipient = await users
				.Find(Builders<UserDocument>.Filter.Regex(u => u.Email, emailPattern))
				.FirstOrDefaultAsync(cancellationToken);

			if (recipient is null)
			{
				return Result<SendFriendRequestResult>.Failure(
					new Error(ErrorCodes.ValidationFailed, "No user exists with that email address."));
			}

			if (recipient.Auth0UserId == command.RequesterUserId)
			{
				return Result<SendFriendRequestResult>.Failure(
					new Error(ErrorCodes.ValidationFailed, "You cannot send a friend request to yourself."));
			}

			var (userAId, userBId) = NormalizePair(command.RequesterUserId, recipient.Auth0UserId);
			var existingFriendship = await friendships
				.Find(f => f.UserAId == userAId && f.UserBId == userBId)
				.FirstOrDefaultAsync(cancellationToken);
			if (existingFriendship is not null)
			{
				return Result<SendFriendRequestResult>.Failure(
					new Error(ErrorCodes.ValidationFailed, "You are already friends with this user."));
			}

			var existingRequest = await requests
				.Find(r => r.RequesterUserId == command.RequesterUserId && r.RecipientUserId == recipient.Auth0UserId)
				.FirstOrDefaultAsync(cancellationToken);
			if (existingRequest is not null)
			{
				return Result<SendFriendRequestResult>.Failure(
					new Error(ErrorCodes.ValidationFailed, "A friend request is already pending for this user."));
			}

			var reciprocalRequest = await requests
				.Find(r => r.RequesterUserId == recipient.Auth0UserId && r.RecipientUserId == command.RequesterUserId)
				.FirstOrDefaultAsync(cancellationToken);

			if (reciprocalRequest is not null)
			{
				var now = DateTime.UtcNow;
				try
				{
					await friendships.InsertOneAsync(new FriendshipDocument
					{
						UserAId = userAId,
						UserBId = userBId,
						CreatedAt = now
					}, cancellationToken: cancellationToken);
				}
				catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
				{
					// Another request may have accepted concurrently; treat as accepted state.
				}

				await requests.DeleteOneAsync(
					r => r.Id == reciprocalRequest.Id,
					cancellationToken);

				return Result<SendFriendRequestResult>.Success(
					new SendFriendRequestResult(
						SendFriendRequestStatus.Accepted,
						recipient.Auth0UserId));
			}

			await requests.InsertOneAsync(new FriendRequestDocument
			{
				RequesterUserId = command.RequesterUserId,
				RecipientUserId = recipient.Auth0UserId,
				CreatedAt = DateTime.UtcNow
			}, cancellationToken: cancellationToken);

			return Result<SendFriendRequestResult>.Success(
				new SendFriendRequestResult(
					SendFriendRequestStatus.Pending,
					recipient.Auth0UserId));
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
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
