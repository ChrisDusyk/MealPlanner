using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

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
/// Handles upserting a user.
/// </summary>
public class UpsertUserFromAuthCommandHandler(MealPlannerDbContext db)
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
			var now = DateTime.UtcNow;
			var email = command.Email.GetValueOrNull();

			var entity = await db.Users
				.FirstOrDefaultAsync(u => u.Auth0UserId == command.Auth0UserId, cancellationToken);

			if (entity is null)
			{
				entity = new UserEntity
				{
					Id = Guid.NewGuid(),
					Auth0UserId = command.Auth0UserId,
					Name = command.Name,
					Email = email,
					CreatedAt = now,
					UpdatedAt = now
				};
				db.Users.Add(entity);
			}
			else
			{
				entity.Email = email;
				entity.UpdatedAt = now;
			}

			await db.SaveChangesAsync(cancellationToken);

			return Result<User>.Success(MapToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<User>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to upsert user.", ex));
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
