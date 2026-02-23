using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

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
public class GetSharesForMealPlanQueryHandler(IMongoClient mongoClient)
	: IQueryHandler<GetSharesForMealPlanQuery, List<ShareWithRecipientInfo>>
{
	public async Task<Result<List<ShareWithRecipientInfo>>> HandleAsync(
		GetSharesForMealPlanQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var db = mongoClient.GetDatabase("mealplannerDb");
			var sharesCollection = db.GetCollection<MealPlanShareDocument>("shares");
			var usersCollection = db.GetCollection<UserDocument>("users");

			var filter = Builders<MealPlanShareDocument>.Filter.And(
				Builders<MealPlanShareDocument>.Filter.Eq(s => s.OwnerUserId, query.OwnerUserId),
				Builders<MealPlanShareDocument>.Filter.Eq(s => s.WeekStart, query.WeekStart));

			var shares = await sharesCollection.Find(filter).ToListAsync(cancellationToken);

			if (shares.Count == 0)
				return Result<List<ShareWithRecipientInfo>>.Success([]);

			// Batch-fetch recipient users
			var recipientIds = shares.Select(s => s.SharedWithUserId).Distinct().ToList();
			var usersFilter = Builders<UserDocument>.Filter.In(u => u.Auth0UserId, recipientIds);
			var users = await usersCollection.Find(usersFilter).ToListAsync(cancellationToken);
			var userLookup = users.ToDictionary(u => u.Auth0UserId);

			var results = shares.Select(s =>
			{
				userLookup.TryGetValue(s.SharedWithUserId, out var user);
				return new ShareWithRecipientInfo(
					Share: s.ToDomain(),
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
