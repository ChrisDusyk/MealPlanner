using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Users.Commands;

/// <summary>
/// Command to update per-friend auto-share preferences.
/// </summary>
public record UpdateFriendAutoSharePreferencesCommand(
	string UserId,
	string FriendUserId,
	bool AutoShareMealPlans,
	bool AutoShareGroceryLists
) : ICommand<UpdateFriendAutoSharePreferencesResult>;

public record UpdateFriendAutoSharePreferencesResult(
	bool AutoShareMealPlans,
	bool AutoShareGroceryLists
);

public class UpdateFriendAutoSharePreferencesCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<UpdateFriendAutoSharePreferencesCommand, UpdateFriendAutoSharePreferencesResult>
{
	public async Task<Result<UpdateFriendAutoSharePreferencesResult>> HandleAsync(
		UpdateFriendAutoSharePreferencesCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.UserId))
			return Result<UpdateFriendAutoSharePreferencesResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "User ID is required."));

		if (string.IsNullOrWhiteSpace(command.FriendUserId))
			return Result<UpdateFriendAutoSharePreferencesResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Friend user ID is required."));

		if (string.Equals(command.UserId, command.FriendUserId, StringComparison.Ordinal))
			return Result<UpdateFriendAutoSharePreferencesResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Friend user ID must be different from user ID."));

		try
		{
			var (userAId, userBId) = SendFriendRequestByEmailCommandHandler.NormalizePair(
				command.UserId,
				command.FriendUserId);

			var existingFriendship = await db.Friendships
				.AnyAsync(f => f.UserAId == userAId && f.UserBId == userBId, cancellationToken);

			if (!existingFriendship)
			{
				return Result<UpdateFriendAutoSharePreferencesResult>.Failure(
					new Error(ErrorCodes.NotFound, "Friendship was not found."));
			}

			var now = DateTime.UtcNow;
			var pref = await db.FriendAutoSharePreferences
				.FirstOrDefaultAsync(p => p.UserId == command.UserId && p.FriendUserId == command.FriendUserId, cancellationToken);

			if (pref is null)
			{
				pref = new FriendAutoSharePreferenceEntity
				{
					Id = Guid.NewGuid(),
					UserId = command.UserId,
					FriendUserId = command.FriendUserId,
					AutoShareMealPlans = command.AutoShareMealPlans,
					AutoShareGroceryLists = command.AutoShareGroceryLists,
					CreatedAt = now,
					UpdatedAt = now
				};
				db.FriendAutoSharePreferences.Add(pref);
			}
			else
			{
				pref.AutoShareMealPlans = command.AutoShareMealPlans;
				pref.AutoShareGroceryLists = command.AutoShareGroceryLists;
				pref.UpdatedAt = now;
			}

			var weekStart = NormalizeToMonday(DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd");
			if (command.AutoShareMealPlans)
				await TryBackfillMealPlanShareAsync(db, command.UserId, command.FriendUserId, weekStart, cancellationToken);

			if (command.AutoShareGroceryLists)
				await TryBackfillGroceryListShareAsync(db, command.UserId, command.FriendUserId, weekStart, cancellationToken);

			await db.SaveChangesAsync(cancellationToken);

			return Result<UpdateFriendAutoSharePreferencesResult>.Success(
				new UpdateFriendAutoSharePreferencesResult(
					command.AutoShareMealPlans,
					command.AutoShareGroceryLists));
		}
		catch (Exception ex)
		{
			return Result<UpdateFriendAutoSharePreferencesResult>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to update friend auto-share preferences.", ex));
		}
	}

	private static async Task TryBackfillMealPlanShareAsync(
		MealPlannerDbContext db,
		string ownerUserId,
		string sharedWithUserId,
		string weekStart,
		CancellationToken cancellationToken)
	{
		var hasPlan = await db.MealPlans
			.AnyAsync(p => p.UserId == ownerUserId && p.WeekStart == weekStart, cancellationToken);
		if (!hasPlan) return;

		var shareExists = await db.MealPlanShares
			.AnyAsync(s => s.OwnerUserId == ownerUserId && s.SharedWithUserId == sharedWithUserId && s.WeekStart == weekStart, cancellationToken);
		if (shareExists) return;

		db.MealPlanShares.Add(new MealPlanShareEntity
		{
			Id = Guid.NewGuid(),
			OwnerUserId = ownerUserId,
			SharedWithUserId = sharedWithUserId,
			WeekStart = weekStart,
			Permission = nameof(SharePermission.ReadWrite),
			SharedAt = DateTime.UtcNow,
			DismissedByRecipient = false
		});
	}

	private static async Task TryBackfillGroceryListShareAsync(
		MealPlannerDbContext db,
		string ownerUserId,
		string sharedWithUserId,
		string weekStart,
		CancellationToken cancellationToken)
	{
		var hasList = await db.GroceryLists
			.AnyAsync(g => g.UserId == ownerUserId && g.WeekStart == weekStart, cancellationToken);
		if (!hasList) return;

		var shareExists = await db.GroceryListShares
			.AnyAsync(s => s.OwnerUserId == ownerUserId && s.SharedWithUserId == sharedWithUserId && s.WeekStart == weekStart, cancellationToken);
		if (shareExists) return;

		db.GroceryListShares.Add(new GroceryListShareEntity
		{
			Id = Guid.NewGuid(),
			OwnerUserId = ownerUserId,
			SharedWithUserId = sharedWithUserId,
			WeekStart = weekStart,
			Permission = nameof(SharePermission.ReadWrite),
			SharedAt = DateTime.UtcNow,
			DismissedByRecipient = false
		});
	}

	private static DateOnly NormalizeToMonday(DateOnly date)
	{
		var dayOfWeek = date.DayOfWeek;
		var offset = dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;
		return date.AddDays(-offset);
	}
}
