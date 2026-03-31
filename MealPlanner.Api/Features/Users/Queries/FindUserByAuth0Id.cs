using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Users.Queries;

/// <summary>
/// Query to find a user by their Auth0 subject identifier.
/// </summary>
public record FindUserByAuth0IdQuery(string Auth0UserId) : IQuery<User>;

/// <summary>
/// Handles looking up a user by Auth0 user id.
/// </summary>
public class FindUserByAuth0IdQueryHandler(MealPlannerDbContext db)
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
			var entity = await db.Users
				.FirstOrDefaultAsync(u => u.Auth0UserId == query.Auth0UserId, cancellationToken);

			if (entity is null)
				return Result<User>.Failure(
					new Error(ErrorCodes.NotFound, "User was not found."));

			return Result<User>.Success(MapToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<User>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to load user profile.", ex));
		}
	}

	internal static User MapToDomain(UserEntity entity) =>
		new(
			Id: entity.Id.ToString(),
			Auth0UserId: entity.Auth0UserId,
			Name: entity.Name,
			Email: Option<string>.From(entity.Email),
			CreatedAt: entity.CreatedAt,
			UpdatedAt: entity.UpdatedAt
		);
}
