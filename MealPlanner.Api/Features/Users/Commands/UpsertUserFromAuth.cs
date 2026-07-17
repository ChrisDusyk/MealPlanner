using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Mappers;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Users.Commands;

/// <summary>
/// Command to create or update a user based on the authenticated identity
/// provider subject (Better Auth user id).
/// </summary>
public record UpsertUserFromAuthCommand(
	string AuthUserId,
	string Name,
	Option<string> Email
) : ICommand<User>;

/// <summary>
/// Handles upserting a user. If no row exists for <see cref="UpsertUserFromAuthCommand.AuthUserId"/>
/// but a user with the same email already exists (for example, after migrating
/// from Auth0 to Better Auth), the existing row is re-linked to the new
/// <c>AuthUserId</c> instead of creating a duplicate account.
/// </summary>
public class UpsertUserFromAuthCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<UpsertUserFromAuthCommand, User>
{
	public async Task<Result<User>> HandleAsync(
		UpsertUserFromAuthCommand command,
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
			var now = DateTime.UtcNow;
			var email = command.Email.GetValueOrNull();

			var entity = await db.Users
				.FirstOrDefaultAsync(u => u.AuthUserId == command.AuthUserId, cancellationToken);

			if (entity is null && !string.IsNullOrWhiteSpace(email))
			{
				// Re-link an existing account migrated from a previous identity
				// provider. Matching on email avoids duplicate user rows.
				entity = await db.Users
					.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
				if (entity is not null)
				{
					entity.AuthUserId = command.AuthUserId;
				}
			}

			if (entity is null)
			{
				entity = new UserEntity
				{
					Id = Guid.NewGuid(),
					AuthUserId = command.AuthUserId,
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

			// Every user always belongs to a family group; provision the
			// single-member personal family eagerly on first sync. The
			// unique index on FamilyGroupMembers.UserId is the source of
			// truth: if a concurrent sync request won the race and already
			// created the membership, treat that as success rather than a
			// failure.
			var hasFamily = await db.FamilyGroupMembers
				.AsNoTracking()
				.AnyAsync(m => m.UserId == command.AuthUserId, cancellationToken);
			if (!hasFamily)
			{
				await Families.FamilyProvisioning.CreatePersonalFamilyAsync(
					db, command.AuthUserId, cancellationToken);
				try
				{
					await db.SaveChangesAsync(cancellationToken);
				}
				catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex))
				{
					// Another request provisioned the family first; discard
					// our local (untracked) entities and continue.
					db.ChangeTracker.Clear();
				}
			}

			return Result<User>.Success(UserMapper.ToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<User>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to upsert user.", ex));
		}
	}

	internal static User MapToDomain(UserEntity entity) => UserMapper.ToDomain(entity);

	private static bool IsUniqueIndexViolation(DbUpdateException ex)
	{
		// Npgsql throws a PostgresException with SqlState 23505 for unique
		// violations. Match by name to avoid taking a Npgsql package reference
		// in tests.
		var inner = ex.InnerException;
		return inner is not null
		       && inner.GetType().Name == "PostgresException"
		       && (inner.GetType().GetProperty("SqlState")?.GetValue(inner) as string) == "23505";
	}
}
