using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Users.Queries;

public record FriendSummary(
	string UserId,
	string Name,
	string? Email,
	bool AutoShareMealPlans,
	bool AutoShareGroceryLists
);

/// <summary>
/// Query to retrieve all accepted friends for a user.
/// </summary>
public record GetFriendsForUserQuery(string UserId) : IQuery<IReadOnlyList<FriendSummary>>;

public class GetFriendsForUserQueryHandler(MealPlannerDbContext db)
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
			var friendshipEntities = await db.Friendships
				.Where(f => f.UserAId == query.UserId || f.UserBId == query.UserId)
				.ToListAsync(cancellationToken);

			var friendUserIds = friendshipEntities
				.Select(f => f.UserAId == query.UserId ? f.UserBId : f.UserAId)
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Distinct(StringComparer.Ordinal)
				.ToList();

			if (friendUserIds.Count == 0)
			{
				return Result<IReadOnlyList<FriendSummary>>.Success(Array.Empty<FriendSummary>());
			}

			var friendEntities = await db.Users
				.Where(u => friendUserIds.Contains(u.Auth0UserId))
				.ToListAsync(cancellationToken);

			var preferenceEntities = await db.FriendAutoSharePreferences
				.Where(p => p.UserId == query.UserId && friendUserIds.Contains(p.FriendUserId))
				.ToListAsync(cancellationToken);

			var preferencesByFriendId = preferenceEntities.ToDictionary(
				p => p.FriendUserId,
				StringComparer.Ordinal);

			var byId = friendEntities.ToDictionary(u => u.Auth0UserId, StringComparer.Ordinal);
			var ordered = friendUserIds
				.Where(byId.ContainsKey)
				.Select(id => MapToSummary(byId[id], preferencesByFriendId.GetValueOrDefault(id)))
				.ToList();

			return Result<IReadOnlyList<FriendSummary>>.Success(ordered);
		}
		catch (Exception ex)
		{
			return Result<IReadOnlyList<FriendSummary>>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to load friends.", ex));
		}
	}

	internal static FriendSummary MapToSummary(UserEntity user, FriendAutoSharePreferenceEntity? preference) =>
		new(
			user.Auth0UserId,
			user.Name,
			user.Email,
			preference?.AutoShareMealPlans ?? false,
			preference?.AutoShareGroceryLists ?? false
		);
}
