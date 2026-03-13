using MealPlanner.Api.Features.Users.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Users;

public static class UserIndexesInitializationExtensions
{
	public static async Task EnsureUserIndexesAsync(
		this IServiceProvider services,
		CancellationToken cancellationToken = default)
	{
		var logger = services.GetRequiredService<ILoggerFactory>()
			.CreateLogger("UserIndexesInitialization");
		var mongoClient = services.GetRequiredService<IMongoClient>();

		try
		{
			var database = mongoClient.GetDatabase("mealplannerDb");
			var users = database.GetCollection<UserDocument>("users");
			var friendships = database.GetCollection<FriendshipDocument>("friendships");
			var requests = database.GetCollection<FriendRequestDocument>("friend_requests");

			await users.Indexes.CreateOneAsync(
				new CreateIndexModel<UserDocument>(
					Builders<UserDocument>.IndexKeys.Ascending(u => u.Auth0UserId),
					new CreateIndexOptions { Unique = true, Name = "ux_users_auth0UserId" }),
				cancellationToken: cancellationToken);

			await friendships.Indexes.CreateOneAsync(
				new CreateIndexModel<FriendshipDocument>(
					Builders<FriendshipDocument>.IndexKeys
						.Ascending(f => f.UserAId)
						.Ascending(f => f.UserBId),
					new CreateIndexOptions { Unique = true, Name = "ux_friendships_pair" }),
				cancellationToken: cancellationToken);

			await requests.Indexes.CreateOneAsync(
				new CreateIndexModel<FriendRequestDocument>(
					Builders<FriendRequestDocument>.IndexKeys
						.Ascending(r => r.RequesterUserId)
						.Ascending(r => r.RecipientUserId),
					new CreateIndexOptions { Unique = true, Name = "ux_friend_requests_direction" }),
				cancellationToken: cancellationToken);

			logger.LogInformation("MongoDB user indexes ensured successfully.");
		}
		catch (MongoCommandException ex) when (ex.CodeName is "IndexOptionsConflict" or "IndexKeySpecsConflict")
		{
			logger.LogError(
				ex,
				"MongoDB index definition conflict while ensuring user indexes. Existing index definitions do not match expected model.");
			throw;
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			logger.LogError(
				ex,
				"MongoDB duplicate key data conflict while ensuring user indexes. Existing data violates a required unique index.");
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Unexpected failure while ensuring MongoDB user indexes.");
			throw;
		}
	}
}
