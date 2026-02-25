using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Users.Commands;

/// <summary>
/// Command to update the current user's name.
/// </summary>
public record UpdateCurrentUserNameCommand(
	string Auth0UserId,
	string Name
) : ICommand<User>;

/// <summary>
/// Handles updating the current user's name in MongoDB.
/// </summary>
public class UpdateCurrentUserNameCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<UpdateCurrentUserNameCommand, User>
{
	public async Task<Result<User>> HandleAsync(
		UpdateCurrentUserNameCommand command,
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

			var updateDefinition = Builders<UserDocument>.Update
				.Set(u => u.Name, command.Name)
				.Set(u => u.UpdatedAt, DateTime.UtcNow);

			var updated = await collection.FindOneAndUpdateAsync(
				u => u.Auth0UserId == command.Auth0UserId,
				updateDefinition,
				new FindOneAndUpdateOptions<UserDocument>
				{
					ReturnDocument = ReturnDocument.After,
					IsUpsert = false
				},
				cancellationToken);

			if (updated is null)
				return Result<User>.Failure(
					new Error(ErrorCodes.NotFound, "User was not found."));

			return Result<User>.Success(MapToDomain(updated));
		}
		catch (Exception ex)
		{
			return Result<User>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to update user profile.", ex));
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
