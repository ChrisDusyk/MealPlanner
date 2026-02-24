using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Users.Queries;

/// <summary>
/// Query to find a user by their email address (case-insensitive).
/// </summary>
public record FindUserByEmailQuery(string Email) : IQuery<User>;

/// <summary>
/// Handles looking up a user by email in MongoDB.
/// </summary>
public class FindUserByEmailQueryHandler(IMongoClient mongoClient)
	: IQueryHandler<FindUserByEmailQuery, User>
{
	public async Task<Result<User>> HandleAsync(
		FindUserByEmailQuery query,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(query.Email))
			return Result<User>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Email is required."));

		try
		{
			var collection = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<UserDocument>("users");

			// Case-insensitive email search
			var filter = Builders<UserDocument>.Filter.Regex(
				u => u.Email,
				new MongoDB.Bson.BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(query.Email)}$", "i"));

			var document = await collection
				.Find(filter)
				.FirstOrDefaultAsync(cancellationToken);

			if (document is null)
				return Result<User>.Failure(
					new Error(ErrorCodes.NotFound, $"No user found with email '{query.Email}'."));

			return Result<User>.Success(MapToDomain(document));
		}
		catch (Exception ex)
		{
			return Result<User>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to search for user.", ex));
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
