using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.MealPlans;

public static class SharingIndexesInitializationExtensions
{
	public static async Task EnsureSharingIndexesAsync(
		this IServiceProvider services,
		CancellationToken cancellationToken = default)
	{
		var logger = services.GetRequiredService<ILoggerFactory>()
			.CreateLogger("SharingIndexesInitialization");
		var mongoClient = services.GetRequiredService<IMongoClient>();

		try
		{
			var database = mongoClient.GetDatabase("mealplannerDb");
			var mealPlanShares = database.GetCollection<MealPlanShareDocument>("shares");
			var groceryListShares = database.GetCollection<GroceryListShareDocument>("grocerylist_shares");

			await mealPlanShares.Indexes.CreateOneAsync(
				new CreateIndexModel<MealPlanShareDocument>(
					Builders<MealPlanShareDocument>.IndexKeys
						.Ascending(s => s.OwnerUserId)
						.Ascending(s => s.SharedWithUserId)
						.Ascending(s => s.WeekStart),
					new CreateIndexOptions { Unique = true, Name = "ux_shares_owner_recipient_week" }),
				cancellationToken: cancellationToken);

			await groceryListShares.Indexes.CreateOneAsync(
				new CreateIndexModel<GroceryListShareDocument>(
					Builders<GroceryListShareDocument>.IndexKeys
						.Ascending(s => s.OwnerUserId)
						.Ascending(s => s.SharedWithUserId)
						.Ascending(s => s.WeekStart),
					new CreateIndexOptions { Unique = true, Name = "ux_grocerylist_shares_owner_recipient_week" }),
				cancellationToken: cancellationToken);

			logger.LogInformation("MongoDB sharing indexes ensured successfully.");
		}
		catch (MongoCommandException ex) when (ex.CodeName is "IndexOptionsConflict" or "IndexKeySpecsConflict")
		{
			logger.LogError(
				ex,
				"MongoDB index definition conflict while ensuring sharing indexes. Existing index definitions do not match expected model.");
			throw;
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			logger.LogError(
				ex,
				"MongoDB duplicate key data conflict while ensuring sharing indexes. Existing data violates a required unique index.");
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Unexpected failure while ensuring MongoDB sharing indexes.");
			throw;
		}
	}
}
