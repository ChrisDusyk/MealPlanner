using MealPlanner.Api.Data;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.MealPlans.Queries;

/// <summary>
/// Query to get all shares created by the owner for a given week.
/// Returns enriched records that include the recipient's name and email.
/// </summary>
public record GetSharesForMealPlanQuery(
	string OwnerUserId,
	string WeekStart
) : IQuery<List<ShareWithRecipientInfo>>;

/// <summary>
/// A share record enriched with the recipient's display info.
/// </summary>
public record ShareWithRecipientInfo(
	MealPlanShare Share,
	string RecipientName,
	string RecipientEmail
);

/// <summary>
/// Handles fetching all shares the owner has created for the specified week,
/// then joins recipient user info for display.
/// </summary>
public class GetSharesForMealPlanQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetSharesForMealPlanQuery, List<ShareWithRecipientInfo>>
{
	public async Task<Result<List<ShareWithRecipientInfo>>> HandleAsync(
		GetSharesForMealPlanQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var shares = await db.MealPlanShares
				.Where(s => s.OwnerUserId == query.OwnerUserId && s.WeekStart == query.WeekStart)
				.ToListAsync(cancellationToken);

			if (shares.Count == 0)
				return Result<List<ShareWithRecipientInfo>>.Success([]);

			// Batch-fetch recipient users
			var recipientIds = shares.Select(s => s.SharedWithUserId).Distinct().ToList();
			var users = await db.Users
				.Where(u => recipientIds.Contains(u.Auth0UserId))
				.ToListAsync(cancellationToken);
			var userLookup = users.ToDictionary(u => u.Auth0UserId);

			var results = shares.Select(s =>
			{
				userLookup.TryGetValue(s.SharedWithUserId, out var user);
				return new ShareWithRecipientInfo(
					Share: GetMealPlanQueryHandler.MapShareToDomain(s),
					RecipientName: user?.Name ?? "Unknown",
					RecipientEmail: user?.Email ?? ""
				);
			}).ToList();

			return Result<List<ShareWithRecipientInfo>>.Success(results);
		}
		catch (Exception ex)
		{
			return Result<List<ShareWithRecipientInfo>>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve shares.", ex));
		}
	}
}
