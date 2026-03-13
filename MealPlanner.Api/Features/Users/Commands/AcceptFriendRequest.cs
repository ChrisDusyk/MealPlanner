using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Users.Commands;

/// <summary>
/// Command to accept an incoming friend request.
/// </summary>
public record AcceptFriendRequestCommand(
	string RecipientUserId,
	string RequestId
) : ICommand<FriendRequestActionResult>;

public record FriendRequestActionResult(string RequesterUserId);

public class AcceptFriendRequestCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<AcceptFriendRequestCommand, FriendRequestActionResult>
{
	public async Task<Result<FriendRequestActionResult>> HandleAsync(
		AcceptFriendRequestCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.RecipientUserId))
			return Result<FriendRequestActionResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipient user ID is required."));

		if (string.IsNullOrWhiteSpace(command.RequestId))
			return Result<FriendRequestActionResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Friend request ID is required."));

		FriendRequestDocument? request = null;

		try
		{
			var database = mongoClient.GetDatabase("mealplannerDb");
			var requests = database.GetCollection<FriendRequestDocument>("friend_requests");
			var friendships = database.GetCollection<FriendshipDocument>("friendships");

			request = await requests
				.Find(r => r.Id == command.RequestId && r.RecipientUserId == command.RecipientUserId)
				.FirstOrDefaultAsync(cancellationToken);
			if (request is null)
			{
				return Result<FriendRequestActionResult>.Failure(
					new Error(ErrorCodes.NotFound, "Friend request was not found."));
			}

			var (userAId, userBId) = SendFriendRequestByEmailCommandHandler.NormalizePair(
				request.RequesterUserId,
				request.RecipientUserId);

			var existingFriendship = await friendships
				.Find(f => f.UserAId == userAId && f.UserBId == userBId)
				.FirstOrDefaultAsync(cancellationToken);

			if (existingFriendship is null)
			{
				await friendships.InsertOneAsync(new FriendshipDocument
				{
					UserAId = userAId,
					UserBId = userBId,
					CreatedAt = DateTime.UtcNow
				}, cancellationToken: cancellationToken);
			}

			await requests.DeleteOneAsync(
				r => r.Id == request.Id,
				cancellationToken);

			await requests.DeleteOneAsync(
				r => r.RequesterUserId == request.RecipientUserId && r.RecipientUserId == request.RequesterUserId,
				cancellationToken);

			return Result<FriendRequestActionResult>.Success(new FriendRequestActionResult(request.RequesterUserId));
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			return Result<FriendRequestActionResult>.Success(
				new FriendRequestActionResult(request?.RequesterUserId ?? string.Empty));
		}
		catch (Exception ex)
		{
			return Result<FriendRequestActionResult>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to accept friend request.", ex));
		}
	}
}
