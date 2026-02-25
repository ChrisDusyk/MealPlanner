using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.GroceryLists.Queries;

/// <summary>
/// Query to get all grocery lists shared with the requesting user for a given week.
/// Returns full list data plus owner name/email for display.
/// </summary>
public record GetGroceryListsSharedWithMeQuery(
	string RecipientUserId,
	string WeekStart
) : IQuery<List<SharedGroceryListResult>>;

/// <summary>
/// A shared grocery list result containing full list data and owner info.
/// </summary>
public record SharedGroceryListResult(
	GroceryListShare Share,
	GroceryList GroceryList,
	string OwnerName,
	string OwnerEmail
);

/// <summary>
/// Handles fetching grocery lists shared with the recipient for the given week.
/// Joins in the grocery list data and owner user info.
/// </summary>
public class GetGroceryListsSharedWithMeQueryHandler(IMongoClient mongoClient)
	: IQueryHandler<GetGroceryListsSharedWithMeQuery, List<SharedGroceryListResult>>
{
	public async Task<Result<List<SharedGroceryListResult>>> HandleAsync(
		GetGroceryListsSharedWithMeQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var db = mongoClient.GetDatabase("mealplannerDb");
			var sharesCollection = db.GetCollection<GroceryListShareDocument>("grocerylist_shares");
			var groceryCollection = db.GetCollection<GroceryListDocument>("grocerylists");
			var usersCollection = db.GetCollection<UserDocument>("users");

			// Get non-dismissed shares for this recipient and week
			var shareFilter = Builders<GroceryListShareDocument>.Filter.And(
				Builders<GroceryListShareDocument>.Filter.Eq(s => s.SharedWithUserId, query.RecipientUserId),
				Builders<GroceryListShareDocument>.Filter.Eq(s => s.WeekStart, query.WeekStart),
				Builders<GroceryListShareDocument>.Filter.Eq(s => s.DismissedByRecipient, false));

			var shares = await sharesCollection.Find(shareFilter).ToListAsync(cancellationToken);

			if (shares.Count == 0)
				return Result<List<SharedGroceryListResult>>.Success([]);

			// Batch-fetch owner users
			var ownerIds = shares.Select(s => s.OwnerUserId).Distinct().ToList();
			var usersFilter = Builders<UserDocument>.Filter.In(u => u.Auth0UserId, ownerIds);
			var owners = await usersCollection.Find(usersFilter).ToListAsync(cancellationToken);
			var ownerLookup = owners.ToDictionary(u => u.Auth0UserId);

			// Batch-fetch grocery lists for the owners and this week
			var listFilter = Builders<GroceryListDocument>.Filter.And(
				Builders<GroceryListDocument>.Filter.In(g => g.UserId, ownerIds),
				Builders<GroceryListDocument>.Filter.Eq(g => g.WeekStart, query.WeekStart));

			var lists = await groceryCollection.Find(listFilter).ToListAsync(cancellationToken);
			var listLookup = lists.ToDictionary(g => g.UserId);

			var results = new List<SharedGroceryListResult>();

			foreach (var share in shares)
			{
				if (!listLookup.TryGetValue(share.OwnerUserId, out var listDoc))
					continue; // Owner hasn't generated a list for this week yet

				ownerLookup.TryGetValue(share.OwnerUserId, out var owner);

				results.Add(new SharedGroceryListResult(
					Share: share.ToDomain(),
					GroceryList: GroceryListHelpers.MapToDomain(listDoc),
					OwnerName: owner?.Name ?? "Unknown",
					OwnerEmail: owner?.Email ?? ""
				));
			}

			return Result<List<SharedGroceryListResult>>.Success(results);
		}
		catch (Exception ex)
		{
			return Result<List<SharedGroceryListResult>>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve shared grocery lists.", ex));
		}
	}
}
