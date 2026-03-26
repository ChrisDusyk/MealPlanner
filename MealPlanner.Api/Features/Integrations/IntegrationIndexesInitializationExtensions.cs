using MealPlanner.Api.Features.Integrations.GoogleKeep.Models;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Integrations;

public static class IntegrationIndexesInitializationExtensions
{
	public static async Task EnsureIntegrationIndexesAsync(
		this IServiceProvider services,
		CancellationToken cancellationToken = default)
	{
		var logger = services.GetRequiredService<ILoggerFactory>()
			.CreateLogger("IntegrationIndexesInitialization");
		var mongoClient = services.GetRequiredService<IMongoClient>();

		try
		{
			var database = mongoClient.GetDatabase("mealplannerDb");
			var connections = database.GetCollection<GoogleIntegrationConnectionDocument>(IntegrationCollections.Connections);
			var exportLinks = database.GetCollection<GroceryListExportLinkDocument>(IntegrationCollections.ExportLinks);

			await connections.Indexes.CreateOneAsync(
				new CreateIndexModel<GoogleIntegrationConnectionDocument>(
					Builders<GoogleIntegrationConnectionDocument>.IndexKeys
						.Ascending(x => x.UserId)
						.Ascending(x => x.Provider),
					new CreateIndexOptions { Unique = true, Name = "ux_google_integrations_user_provider" }),
				cancellationToken: cancellationToken);

			await connections.Indexes.CreateOneAsync(
				new CreateIndexModel<GoogleIntegrationConnectionDocument>(
					Builders<GoogleIntegrationConnectionDocument>.IndexKeys
						.Ascending(x => x.GoogleSubject)
						.Ascending(x => x.Provider),
					new CreateIndexOptions { Unique = true, Name = "ux_google_integrations_subject_provider" }),
				cancellationToken: cancellationToken);

			await exportLinks.Indexes.CreateOneAsync(
				new CreateIndexModel<GroceryListExportLinkDocument>(
					Builders<GroceryListExportLinkDocument>.IndexKeys
						.Ascending(x => x.UserId)
						.Ascending(x => x.WeekStart)
						.Ascending(x => x.Provider),
					new CreateIndexOptions { Unique = true, Name = "ux_export_links_user_week_provider" }),
				cancellationToken: cancellationToken);

			await exportLinks.Indexes.CreateOneAsync(
				new CreateIndexModel<GroceryListExportLinkDocument>(
					Builders<GroceryListExportLinkDocument>.IndexKeys
						.Ascending(x => x.UserId)
						.Ascending(x => x.GroceryListId)
						.Ascending(x => x.Provider),
					new CreateIndexOptions { Unique = true, Name = "ux_export_links_user_grocery_provider" }),
				cancellationToken: cancellationToken);

			logger.LogInformation("MongoDB integration indexes ensured successfully.");
		}
		catch (MongoCommandException ex) when (ex.CodeName is "IndexOptionsConflict" or "IndexKeySpecsConflict")
		{
			logger.LogError(
				ex,
				"MongoDB index definition conflict while ensuring integration indexes. Existing index definitions do not match expected model.");
			throw;
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			logger.LogError(
				ex,
				"MongoDB duplicate key data conflict while ensuring integration indexes. Existing data violates a required unique index.");
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Unexpected failure while ensuring MongoDB integration indexes.");
			throw;
		}
	}
}
