using MealPlanner.Api.Data;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.MealPlans.Queries;

/// <summary>
/// Query to get all meal plans shared with the requesting user for a given week.
/// Returns full plan data plus owner name/email for display.
/// </summary>
public record GetSharedWithMeQuery(
	string RecipientUserId,
	string WeekStart
) : IQuery<List<SharedMealPlanResult>>;

/// <summary>
/// A shared meal plan result containing full plan data and owner info.
/// </summary>
public record SharedMealPlanResult(
	MealPlanShare Share,
	MealPlan MealPlan,
	string OwnerName,
	string OwnerEmail
);

/// <summary>
/// Handles fetching plans shared with the recipient for the given week.
/// Joins in the meal plan data and owner user info.
/// </summary>
public class GetSharedWithMeQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetSharedWithMeQuery, List<SharedMealPlanResult>>
{
	public async Task<Result<List<SharedMealPlanResult>>> HandleAsync(
		GetSharedWithMeQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			// Get non-dismissed shares for this recipient and week
			var shares = await db.MealPlanShares
				.Where(s => s.SharedWithUserId == query.RecipientUserId
					&& s.WeekStart == query.WeekStart
					&& !s.DismissedByRecipient)
				.ToListAsync(cancellationToken);

			if (shares.Count == 0)
				return Result<List<SharedMealPlanResult>>.Success([]);

			// Batch-fetch owner users
			var ownerIds = shares.Select(s => s.OwnerUserId).Distinct().ToList();
			var owners = await db.Users
				.Where(u => ownerIds.Contains(u.AuthUserId))
				.ToListAsync(cancellationToken);
			var ownerLookup = owners.ToDictionary(u => u.AuthUserId);

			// Batch-fetch meal plans for the owners and this week
			var plans = await db.MealPlans
				.Where(p => ownerIds.Contains(p.UserId) && p.WeekStart == query.WeekStart)
				.ToListAsync(cancellationToken);
			var planLookup = plans.ToDictionary(p => p.UserId);

			var results = new List<SharedMealPlanResult>();

			foreach (var share in shares)
			{
				if (!planLookup.TryGetValue(share.OwnerUserId, out var planDoc))
					continue; // Owner hasn't created a plan for this week yet

				ownerLookup.TryGetValue(share.OwnerUserId, out var owner);

				results.Add(new SharedMealPlanResult(
					Share: GetMealPlanQueryHandler.MapShareToDomain(share),
					MealPlan: GetMealPlanQueryHandler.MapToDomain(planDoc),
					OwnerName: owner?.Name ?? "Unknown",
					OwnerEmail: owner?.Email ?? ""
				));
			}

			return Result<List<SharedMealPlanResult>>.Success(results);
		}
		catch (Exception ex)
		{
			return Result<List<SharedMealPlanResult>>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve shared meal plans.", ex));
		}
	}
}
