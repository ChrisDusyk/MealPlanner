using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Users.Queries;

/// <summary>
/// Query to retrieve outgoing pending friend requests for a user.
/// </summary>
public record GetOutgoingFriendRequestsQuery(string RequesterUserId) : IQuery<IReadOnlyList<FriendRequestSummary>>;

public class GetOutgoingFriendRequestsQueryHandler(IMongoClient mongoClient)
	: IQueryHandler<GetOutgoingFriendRequestsQuery, IReadOnlyList<FriendRequestSummary>>
{
	public async Task<Result<IReadOnlyList<FriendRequestSummary>>> HandleAsync(
		GetOutgoingFriendRequestsQuery query,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(query.RequesterUserId))
			return Result<IReadOnlyList<FriendRequestSummary>>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Requester user ID is required."));

		try
		{
			var database = mongoClient.GetDatabase("mealplannerDb");
			var requests = database.GetCollection<FriendRequestDocument>("friend_requests");
			var users = database.GetCollection<UserDocument>("users");

			var outgoing = await requests
				.Find(r => r.RequesterUserId == query.RequesterUserId)
				.SortByDescending(r => r.CreatedAt)
				.ToListAsync(cancellationToken);

			if (outgoing.Count == 0)
			{
				return Result<IReadOnlyList<FriendRequestSummary>>.Success(Array.Empty<FriendRequestSummary>());
			}

			var recipientIds = outgoing
				.Select(r => r.RecipientUserId)
				.Distinct(StringComparer.Ordinal)
				.ToList();

			var recipientDocs = await users
				.Find(u => recipientIds.Contains(u.Auth0UserId))
				.ToListAsync(cancellationToken);
			var recipientById = recipientDocs.ToDictionary(u => u.Auth0UserId, StringComparer.Ordinal);

			var summaries = outgoing
				.Where(r => r.Id is not null && recipientById.ContainsKey(r.RecipientUserId))
				.Select(r =>
				{
					var recipient = recipientById[r.RecipientUserId];
					return new FriendRequestSummary(
						r.Id!,
						recipient.Auth0UserId,
						recipient.Name,
						recipient.Email,
						r.CreatedAt);
				})
				.ToList();

			return Result<IReadOnlyList<FriendRequestSummary>>.Success(summaries);
		}
		catch (Exception ex)
		{
			return Result<IReadOnlyList<FriendRequestSummary>>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to load outgoing friend requests.", ex));
		}
	}
}
