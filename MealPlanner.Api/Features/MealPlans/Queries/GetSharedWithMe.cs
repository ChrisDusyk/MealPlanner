using MealPlanner.Api.Features.MealPlans.Dtos;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

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
public class GetSharedWithMeQueryHandler(IMongoClient mongoClient)
	: IQueryHandler<GetSharedWithMeQuery, List<SharedMealPlanResult>>
{
	public async Task<Result<List<SharedMealPlanResult>>> HandleAsync(
		GetSharedWithMeQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var db = mongoClient.GetDatabase("mealplannerDb");
			var sharesCollection = db.GetCollection<MealPlanShareDocument>("shares");
			var plansCollection = db.GetCollection<MealPlanDocument>("mealplans");
			var usersCollection = db.GetCollection<UserDocument>("users");

			// Get non-dismissed shares for this recipient and week
			var shareFilter = Builders<MealPlanShareDocument>.Filter.And(
				Builders<MealPlanShareDocument>.Filter.Eq(s => s.SharedWithUserId, query.RecipientUserId),
				Builders<MealPlanShareDocument>.Filter.Eq(s => s.WeekStart, query.WeekStart),
				Builders<MealPlanShareDocument>.Filter.Eq(s => s.DismissedByRecipient, false));

			var shares = await sharesCollection.Find(shareFilter).ToListAsync(cancellationToken);

			if (shares.Count == 0)
				return Result<List<SharedMealPlanResult>>.Success([]);

			// Batch-fetch owner users
			var ownerIds = shares.Select(s => s.OwnerUserId).Distinct().ToList();
			var usersFilter = Builders<UserDocument>.Filter.In(u => u.Auth0UserId, ownerIds);
			var owners = await usersCollection.Find(usersFilter).ToListAsync(cancellationToken);
			var ownerLookup = owners.ToDictionary(u => u.Auth0UserId);

			// Batch-fetch meal plans for the owners and this week
			var planFilter = Builders<MealPlanDocument>.Filter.And(
				Builders<MealPlanDocument>.Filter.In(p => p.UserId, ownerIds),
				Builders<MealPlanDocument>.Filter.Eq(p => p.WeekStart, query.WeekStart));

			var plans = await plansCollection.Find(planFilter).ToListAsync(cancellationToken);
			var planLookup = plans.ToDictionary(p => p.UserId);

			var results = new List<SharedMealPlanResult>();

			foreach (var share in shares)
			{
				if (!planLookup.TryGetValue(share.OwnerUserId, out var planDoc))
					continue; // Owner hasn't created a plan for this week yet

				ownerLookup.TryGetValue(share.OwnerUserId, out var owner);

				results.Add(new SharedMealPlanResult(
					Share: share.ToDomain(),
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
