using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Mappers;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Users.Queries;

/// <summary>
/// Query to find a user by their Better Auth subject identifier.
/// </summary>
public record FindUserByAuthIdQuery(string AuthUserId) : IQuery<User>;

/// <summary>
/// Handles looking up a user by their Better Auth user id.
/// </summary>
public class FindUserByAuthIdQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<FindUserByAuthIdQuery, User>
{
	public async Task<Result<User>> HandleAsync(
		FindUserByAuthIdQuery query,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(query.AuthUserId))
			return Result<User>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Auth user ID is required."));

		try
		{
			var entity = await db.Users
				.FirstOrDefaultAsync(u => u.AuthUserId == query.AuthUserId, cancellationToken);

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

	internal static User MapToDomain(UserEntity entity) => UserMapper.ToDomain(entity);
}
