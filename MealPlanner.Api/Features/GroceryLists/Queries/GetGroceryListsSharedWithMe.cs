using MealPlanner.Api.Data;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

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
public class GetGroceryListsSharedWithMeQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetGroceryListsSharedWithMeQuery, List<SharedGroceryListResult>>
{
	public async Task<Result<List<SharedGroceryListResult>>> HandleAsync(
		GetGroceryListsSharedWithMeQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			// Get non-dismissed shares for this recipient and week
			var shares = await db.GroceryListShares
				.Where(s => s.SharedWithUserId == query.RecipientUserId
					&& s.WeekStart == query.WeekStart
					&& !s.DismissedByRecipient)
				.ToListAsync(cancellationToken);

			if (shares.Count == 0)
				return Result<List<SharedGroceryListResult>>.Success([]);

			// Batch-fetch owner users
			var ownerIds = shares.Select(s => s.OwnerUserId).Distinct().ToList();
			var owners = await db.Users
				.Where(u => ownerIds.Contains(u.Auth0UserId))
				.ToListAsync(cancellationToken);
			var ownerLookup = owners.ToDictionary(u => u.Auth0UserId);

			// Batch-fetch grocery lists for the owners and this week
			var lists = await db.GroceryLists
				.Where(g => ownerIds.Contains(g.UserId) && g.WeekStart == query.WeekStart)
				.ToListAsync(cancellationToken);
			var listLookup = lists.ToDictionary(g => g.UserId);

			var results = new List<SharedGroceryListResult>();

			foreach (var share in shares)
			{
				if (!listLookup.TryGetValue(share.OwnerUserId, out var listDoc))
					continue; // Owner hasn't generated a list for this week yet

				ownerLookup.TryGetValue(share.OwnerUserId, out var owner);

				results.Add(new SharedGroceryListResult(
					Share: GroceryListHelpers.MapShareToDomain(share),
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
