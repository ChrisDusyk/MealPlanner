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
	private static readonly SemaphoreSlim IndexCreationLock = new(1, 1);
	private static bool _indexCreated;

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

			var existing = await collection
				.Find(u => u.Auth0UserId == command.Auth0UserId)
				.FirstOrDefaultAsync(cancellationToken);

			var now = DateTime.UtcNow;
			if (existing is null)
			{
				var created = new UserDocument
				{
					Auth0UserId = command.Auth0UserId,
					Name = command.Name,
					Email = command.Email.GetValueOrNull(),
					CreatedAt = now,
					UpdatedAt = now
				};

				await collection.InsertOneAsync(created, cancellationToken: cancellationToken);
				return Result<User>.Success(MapToDomain(created));
			}

			existing.Name = command.Name;
			existing.Email = command.Email.GetValueOrNull();
			existing.UpdatedAt = now;

			await collection.ReplaceOneAsync(
				u => u.Id == existing.Id,
				existing,
				cancellationToken: cancellationToken);

			return Result<User>.Success(MapToDomain(existing));
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
		if (_indexCreated)
			return;

		await IndexCreationLock.WaitAsync(cancellationToken);
		try
		{
			if (_indexCreated)
				return;

			var indexModel = new CreateIndexModel<UserDocument>(
				Builders<UserDocument>.IndexKeys.Ascending(u => u.Auth0UserId),
				new CreateIndexOptions { Unique = true, Name = "ux_users_auth0UserId" });

			await collection.Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
			_indexCreated = true;
		}
		finally
		{
			IndexCreationLock.Release();
		}
	}

	private static User MapToDomain(UserDocument document) =>
		new(
			Id: document.Id ?? string.Empty,
			Auth0UserId: document.Auth0UserId,
			Name: document.Name,
			Email: Option<string>.From(document.Email),
			CreatedAt: document.CreatedAt,
			UpdatedAt: document.UpdatedAt
		);
}
