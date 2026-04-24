using MealPlanner.Api.Data;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

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
public class GetSharesForGroceryListQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetSharesForGroceryListQuery, List<GroceryListShareWithRecipientInfo>>
{
	public async Task<Result<List<GroceryListShareWithRecipientInfo>>> HandleAsync(
		GetSharesForGroceryListQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var shares = await db.GroceryListShares
				.Where(s => s.OwnerUserId == query.OwnerUserId && s.WeekStart == query.WeekStart)
				.ToListAsync(cancellationToken);

			if (shares.Count == 0)
				return Result<List<GroceryListShareWithRecipientInfo>>.Success([]);

			// Batch-fetch recipient users
			var recipientIds = shares.Select(s => s.SharedWithUserId).Distinct().ToList();
			var users = await db.Users
				.Where(u => recipientIds.Contains(u.AuthUserId))
				.ToListAsync(cancellationToken);
			var userLookup = users.ToDictionary(u => u.AuthUserId);

			var results = shares.Select(s =>
			{
				userLookup.TryGetValue(s.SharedWithUserId, out var user);
				return new GroceryListShareWithRecipientInfo(
					Share: GroceryListHelpers.MapShareToDomain(s),
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
