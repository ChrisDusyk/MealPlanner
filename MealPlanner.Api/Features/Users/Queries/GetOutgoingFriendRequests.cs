using MealPlanner.Api.Data;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Users.Queries;

/// <summary>
/// Query to retrieve outgoing pending friend requests for a user.
/// </summary>
public record GetOutgoingFriendRequestsQuery(string RequesterUserId) : IQuery<IReadOnlyList<FriendRequestSummary>>;

public class GetOutgoingFriendRequestsQueryHandler(MealPlannerDbContext db)
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
			var outgoing = await db.FriendRequests
				.Where(r => r.RequesterUserId == query.RequesterUserId)
				.OrderByDescending(r => r.CreatedAt)
				.ToListAsync(cancellationToken);

			if (outgoing.Count == 0)
			{
				return Result<IReadOnlyList<FriendRequestSummary>>.Success(Array.Empty<FriendRequestSummary>());
			}

			var recipientIds = outgoing
				.Select(r => r.RecipientUserId)
				.Distinct(StringComparer.Ordinal)
				.ToList();

			var recipientEntities = await db.Users
				.Where(u => recipientIds.Contains(u.AuthUserId))
				.ToListAsync(cancellationToken);
			var recipientById = recipientEntities.ToDictionary(u => u.AuthUserId, StringComparer.Ordinal);

			var summaries = outgoing
				.Where(r => recipientById.ContainsKey(r.RecipientUserId))
				.Select(r =>
				{
					var recipient = recipientById[r.RecipientUserId];
					return new FriendRequestSummary(
						r.Id.ToString(),
						recipient.AuthUserId,
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
