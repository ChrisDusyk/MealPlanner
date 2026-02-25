using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Users.Queries;

/// <summary>
/// Query to find a user by their Auth0 subject identifier.
/// </summary>
public record FindUserByAuth0IdQuery(string Auth0UserId) : IQuery<User>;

/// <summary>
/// Handles looking up a user by Auth0 user id in MongoDB.
/// </summary>
public class FindUserByAuth0IdQueryHandler(IMongoClient mongoClient)
	: IQueryHandler<FindUserByAuth0IdQuery, User>
{
	public async Task<Result<User>> HandleAsync(
		FindUserByAuth0IdQuery query,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(query.Auth0UserId))
			return Result<User>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Auth0 user ID is required."));

		try
		{
			var collection = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<UserDocument>("users");

			var document = await collection
				.Find(u => u.Auth0UserId == query.Auth0UserId)
				.FirstOrDefaultAsync(cancellationToken);

			if (document is null)
				return Result<User>.Failure(
					new Error(ErrorCodes.NotFound, "User was not found."));

			return Result<User>.Success(MapToDomain(document));
		}
		catch (Exception ex)
		{
			return Result<User>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to load user profile.", ex));
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
