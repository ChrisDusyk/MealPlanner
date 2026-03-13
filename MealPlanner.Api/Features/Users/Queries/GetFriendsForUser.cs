using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Users.Queries;

public record FriendSummary(string UserId, string Name, string? Email);

/// <summary>
/// Query to retrieve all accepted friends for a user.
/// </summary>
public record GetFriendsForUserQuery(string UserId) : IQuery<IReadOnlyList<FriendSummary>>;

public class GetFriendsForUserQueryHandler(IMongoClient mongoClient)
	: IQueryHandler<GetFriendsForUserQuery, IReadOnlyList<FriendSummary>>
{
	public async Task<Result<IReadOnlyList<FriendSummary>>> HandleAsync(
		GetFriendsForUserQuery query,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(query.UserId))
			return Result<IReadOnlyList<FriendSummary>>.Failure(
				new Error(ErrorCodes.ValidationFailed, "User ID is required."));

		try
		{
			var database = mongoClient.GetDatabase("mealplannerDb");
			var friendships = database.GetCollection<FriendshipDocument>("friendships");
			var users = database.GetCollection<UserDocument>("users");

			var friendshipDocs = await friendships
				.Find(f => f.UserAId == query.UserId || f.UserBId == query.UserId)
				.ToListAsync(cancellationToken);

			var friendUserIds = friendshipDocs
				.Select(f => f.UserAId == query.UserId ? f.UserBId : f.UserAId)
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Distinct(StringComparer.Ordinal)
				.ToList();

			if (friendUserIds.Count == 0)
			{
				return Result<IReadOnlyList<FriendSummary>>.Success(Array.Empty<FriendSummary>());
			}

			var friendDocs = await users
				.Find(u => friendUserIds.Contains(u.Auth0UserId))
				.ToListAsync(cancellationToken);

			var byId = friendDocs.ToDictionary(u => u.Auth0UserId, StringComparer.Ordinal);
			var ordered = friendUserIds
				.Where(byId.ContainsKey)
				.Select(id => byId[id])
				.Select(MapToSummary)
				.ToList();

			return Result<IReadOnlyList<FriendSummary>>.Success(ordered);
		}
		catch (Exception ex)
		{
			return Result<IReadOnlyList<FriendSummary>>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to load friends.", ex));
		}
	}

	internal static FriendSummary MapToSummary(UserDocument user) =>
		new(user.Auth0UserId, user.Name, user.Email);
}
