using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Users.Commands;

/// <summary>
/// Command to create or update a user based on Auth0 identity.
/// </summary>
public record UpsertUserFromAuthCommand(
	string Auth0UserId,
	string Name,
	Option<string> Email
) : ICommand<User>;

/// <summary>
/// Handles upserting a user in MongoDB.
/// </summary>
public class UpsertUserFromAuthCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<UpsertUserFromAuthCommand, User>
{
	public async Task<Result<User>> HandleAsync(
		UpsertUserFromAuthCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.Auth0UserId))
			return Result<User>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Auth0 user ID is required."));

		if (string.IsNullOrWhiteSpace(command.Name))
			return Result<User>.Failure(
				new Error(ErrorCodes.ValidationFailed, "User name is required."));

		try
		{
			var collection = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<UserDocument>("users");

			await EnsureIndexesAsync(collection, cancellationToken);

			var now = DateTime.UtcNow;
			var email = command.Email.GetValueOrNull();

			var updateDefinition = Builders<UserDocument>.Update
				.Set(u => u.Name, command.Name)
				.Set(u => u.Email, email)
				.Set(u => u.UpdatedAt, now)
				.SetOnInsert(u => u.Auth0UserId, command.Auth0UserId)
				.SetOnInsert(u => u.CreatedAt, now);

			var options = new FindOneAndUpdateOptions<UserDocument>
			{
				IsUpsert = true,
				ReturnDocument = ReturnDocument.After
			};

			var updated = await collection.FindOneAndUpdateAsync(
				u => u.Auth0UserId == command.Auth0UserId,
				updateDefinition,
				options,
				cancellationToken);

			if (updated is null)
			{
				return Result<User>.Failure(
					new Error(ErrorCodes.DatabaseError, "Failed to upsert user."));
			}

			return Result<User>.Success(MapToDomain(updated));
		}
		catch (Exception ex)
		{
			return Result<User>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to upsert user.", ex));
		}
	}

	private static async Task EnsureIndexesAsync(
		IMongoCollection<UserDocument> collection,
		CancellationToken cancellationToken)
	{
		try
		{
			var indexModel = new CreateIndexModel<UserDocument>(
				Builders<UserDocument>.IndexKeys.Ascending(u => u.Auth0UserId),
				new CreateIndexOptions { Unique = true, Name = "ux_users_auth0UserId" });

			await collection.Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
		}
		catch (MongoCommandException ex) when (ex.CodeName is "IndexOptionsConflict" or "IndexKeySpecsConflict")
		{
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
		}
	}

	internal static User MapToDomain(UserDocument document) =>
		new(
			Id: document.Id ?? string.Empty,
			Auth0UserId: document.Auth0UserId,
			Name: document.Name,
			Email: Option<string>.From(document.Email),
			CreatedAt: document.CreatedAt,
			UpdatedAt: document.UpdatedAt
		);
}
