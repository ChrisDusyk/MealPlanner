using MealPlanner.Api.Data;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Users.Queries;

public record FriendRequestSummary(
	string RequestId,
	string UserId,
	string Name,
	string? Email,
	DateTime CreatedAt);

/// <summary>
/// Query to retrieve incoming pending friend requests for a user.
/// </summary>
public record GetIncomingFriendRequestsQuery(string RecipientUserId) : IQuery<IReadOnlyList<FriendRequestSummary>>;

public class GetIncomingFriendRequestsQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetIncomingFriendRequestsQuery, IReadOnlyList<FriendRequestSummary>>
{
	public async Task<Result<IReadOnlyList<FriendRequestSummary>>> HandleAsync(
		GetIncomingFriendRequestsQuery query,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(query.RecipientUserId))
			return Result<IReadOnlyList<FriendRequestSummary>>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipient user ID is required."));

		try
		{
			var incoming = await db.FriendRequests
				.Where(r => r.RecipientUserId == query.RecipientUserId)
				.OrderByDescending(r => r.CreatedAt)
				.ToListAsync(cancellationToken);

			if (incoming.Count == 0)
			{
				return Result<IReadOnlyList<FriendRequestSummary>>.Success(Array.Empty<FriendRequestSummary>());
			}

			var requesterIds = incoming
				.Select(r => r.RequesterUserId)
				.Distinct(StringComparer.Ordinal)
				.ToList();

			var requesterEntities = await db.Users
				.Where(u => requesterIds.Contains(u.Auth0UserId))
				.ToListAsync(cancellationToken);
			var requesterById = requesterEntities.ToDictionary(u => u.Auth0UserId, StringComparer.Ordinal);

			var summaries = incoming
				.Where(r => requesterById.ContainsKey(r.RequesterUserId))
				.Select(r =>
				{
					var requester = requesterById[r.RequesterUserId];
					return new FriendRequestSummary(
						r.Id.ToString(),
						requester.Auth0UserId,
						requester.Name,
						requester.Email,
						r.CreatedAt);
				})
				.ToList();

			return Result<IReadOnlyList<FriendRequestSummary>>.Success(summaries);
		}
		catch (Exception ex)
		{
			return Result<IReadOnlyList<FriendRequestSummary>>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to load incoming friend requests.", ex));
		}
	}
}
