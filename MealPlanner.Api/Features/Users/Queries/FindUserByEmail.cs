using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Users.Queries;

/// <summary>
/// Query to find a user by their email address (case-insensitive).
/// </summary>
public record FindUserByEmailQuery(string Email) : IQuery<User>;

/// <summary>
/// Handles looking up a user by email.
/// </summary>
public class FindUserByEmailQueryHandler(MealPlannerDbContext db)
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
			// Case-insensitive email search
			var normalizedEmail = query.Email.Trim().ToUpperInvariant();
			var entity = await db.Users
				.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToUpper() == normalizedEmail, cancellationToken);

			if (entity is null)
				return Result<User>.Failure(
					new Error(ErrorCodes.NotFound, $"No user found with email '{query.Email}'."));

			return Result<User>.Success(MapToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<User>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to search for user.", ex));
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
