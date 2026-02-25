using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.GroceryLists.Queries;

/// <summary>
/// Query to get all shares created by the owner for a given week.
/// Returns enriched records that include the recipient's name and email.
/// </summary>
public record GetSharesForGroceryListQuery(
	string OwnerUserId,
	string WeekStart
) : IQuery<List<GroceryListShareWithRecipientInfo>>;

/// <summary>
/// A grocery list share record enriched with the recipient's display info.
/// </summary>
public record GroceryListShareWithRecipientInfo(
	GroceryListShare Share,
	string RecipientName,
	string RecipientEmail
);

/// <summary>
/// Handles fetching all grocery list shares the owner has created for the specified week,
/// then joins recipient user info for display.
/// </summary>
public class GetSharesForGroceryListQueryHandler(IMongoClient mongoClient)
	: IQueryHandler<GetSharesForGroceryListQuery, List<GroceryListShareWithRecipientInfo>>
{
	public async Task<Result<List<GroceryListShareWithRecipientInfo>>> HandleAsync(
		GetSharesForGroceryListQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var db = mongoClient.GetDatabase("mealplannerDb");
			var sharesCollection = db.GetCollection<GroceryListShareDocument>("grocerylist_shares");
			var usersCollection = db.GetCollection<UserDocument>("users");

			var filter = Builders<GroceryListShareDocument>.Filter.And(
				Builders<GroceryListShareDocument>.Filter.Eq(s => s.OwnerUserId, query.OwnerUserId),
				Builders<GroceryListShareDocument>.Filter.Eq(s => s.WeekStart, query.WeekStart));

			var shares = await sharesCollection.Find(filter).ToListAsync(cancellationToken);

			if (shares.Count == 0)
				return Result<List<GroceryListShareWithRecipientInfo>>.Success([]);

			// Batch-fetch recipient users
			var recipientIds = shares.Select(s => s.SharedWithUserId).Distinct().ToList();
			var usersFilter = Builders<UserDocument>.Filter.In(u => u.Auth0UserId, recipientIds);
			var users = await usersCollection.Find(usersFilter).ToListAsync(cancellationToken);
			var userLookup = users.ToDictionary(u => u.Auth0UserId);

			var results = shares.Select(s =>
			{
				userLookup.TryGetValue(s.SharedWithUserId, out var user);
				return new GroceryListShareWithRecipientInfo(
					Share: s.ToDomain(),
					RecipientName: user?.Name ?? "Unknown",
					RecipientEmail: user?.Email ?? ""
				);
			}).ToList();

			return Result<List<GroceryListShareWithRecipientInfo>>.Success(results);
		}
		catch (Exception ex)
		{
			return Result<List<GroceryListShareWithRecipientInfo>>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve grocery list shares.", ex));
		}
	}
}
