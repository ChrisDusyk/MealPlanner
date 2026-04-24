using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Mappers;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Users.Commands;

/// <summary>
/// Command to update the current user's name.
/// </summary>
public record UpdateCurrentUserNameCommand(
	string AuthUserId,
	string Name
) : ICommand<User>;

/// <summary>
/// Handles updating the current user's name.
/// </summary>
public class UpdateCurrentUserNameCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<UpdateCurrentUserNameCommand, User>
{
	public async Task<Result<User>> HandleAsync(
		UpdateCurrentUserNameCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.AuthUserId))
			return Result<User>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Auth user ID is required."));

		if (string.IsNullOrWhiteSpace(command.Name))
			return Result<User>.Failure(
				new Error(ErrorCodes.ValidationFailed, "User name is required."));

		try
		{
			var entity = await db.Users
				.FirstOrDefaultAsync(u => u.AuthUserId == command.AuthUserId, cancellationToken);

			if (entity is null)
				return Result<User>.Failure(
					new Error(ErrorCodes.NotFound, "User was not found."));

			entity.Name = command.Name;
			entity.UpdatedAt = DateTime.UtcNow;

			await db.SaveChangesAsync(cancellationToken);

			return Result<User>.Success(MapToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<User>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to update user profile.", ex));
		}
	}

	internal static User MapToDomain(UserEntity entity) => UserMapper.ToDomain(entity);
}
