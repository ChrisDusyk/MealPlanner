using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Users.Commands;

/// <summary>
/// Command to accept an incoming friend request.
/// </summary>
public record AcceptFriendRequestCommand(
	string RecipientUserId,
	string RequestId
) : ICommand<FriendRequestActionResult>;

public record FriendRequestActionResult(string RequesterUserId);

public class AcceptFriendRequestCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<AcceptFriendRequestCommand, FriendRequestActionResult>
{
	public async Task<Result<FriendRequestActionResult>> HandleAsync(
		AcceptFriendRequestCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.RecipientUserId))
			return Result<FriendRequestActionResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipient user ID is required."));

		if (string.IsNullOrWhiteSpace(command.RequestId))
			return Result<FriendRequestActionResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Friend request ID is required."));

		if (!Guid.TryParse(command.RequestId, out var requestGuid))
			return Result<FriendRequestActionResult>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Friend request ID is invalid."));

		FriendRequestEntity? request = null;

		try
		{
			request = await db.FriendRequests
				.FirstOrDefaultAsync(r => r.Id == requestGuid && r.RecipientUserId == command.RecipientUserId, cancellationToken);
			if (request is null)
			{
				return Result<FriendRequestActionResult>.Failure(
					new Error(ErrorCodes.NotFound, "Friend request was not found."));
			}

			var (userAId, userBId) = SendFriendRequestByEmailCommandHandler.NormalizePair(
				request.RequesterUserId,
				request.RecipientUserId);

			var existingFriendship = await db.Friendships
				.AnyAsync(f => f.UserAId == userAId && f.UserBId == userBId, cancellationToken);

			if (!existingFriendship)
			{
				db.Friendships.Add(new FriendshipEntity
				{
					Id = Guid.NewGuid(),
					UserAId = userAId,
					UserBId = userBId,
					CreatedAt = DateTime.UtcNow
				});
			}

			await EnsureDefaultPreferenceAsync(db, request.RequesterUserId, request.RecipientUserId, cancellationToken);
			await EnsureDefaultPreferenceAsync(db, request.RecipientUserId, request.RequesterUserId, cancellationToken);

			await BackfillCurrentWeekSharesAsync(db, request.RequesterUserId, request.RecipientUserId, cancellationToken);

			db.FriendRequests.Remove(request);

			var reciprocal = await db.FriendRequests
				.FirstOrDefaultAsync(r => r.RequesterUserId == request.RecipientUserId && r.RecipientUserId == request.RequesterUserId, cancellationToken);
			if (reciprocal is not null)
				db.FriendRequests.Remove(reciprocal);

			await db.SaveChangesAsync(cancellationToken);

			return Result<FriendRequestActionResult>.Success(new FriendRequestActionResult(request.RequesterUserId));
		}
		catch (DbUpdateException)
		{
			return Result<FriendRequestActionResult>.Success(
				new FriendRequestActionResult(request?.RequesterUserId ?? string.Empty));
		}
		catch (Exception ex)
		{
			return Result<FriendRequestActionResult>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to accept friend request.", ex));
		}
	}

	private static async Task EnsureDefaultPreferenceAsync(
		MealPlannerDbContext db,
		string userId,
		string friendUserId,
		CancellationToken cancellationToken)
	{
		var exists = await db.FriendAutoSharePreferences
			.AnyAsync(p => p.UserId == userId && p.FriendUserId == friendUserId, cancellationToken);

		if (!exists)
		{
			var now = DateTime.UtcNow;
			db.FriendAutoSharePreferences.Add(new FriendAutoSharePreferenceEntity
			{
				Id = Guid.NewGuid(),
				UserId = userId,
				FriendUserId = friendUserId,
				AutoShareMealPlans = false,
				AutoShareGroceryLists = false,
				CreatedAt = now,
				UpdatedAt = now
			});
		}
	}

	private static async Task BackfillCurrentWeekSharesAsync(
		MealPlannerDbContext db,
		string requesterUserId,
		string recipientUserId,
		CancellationToken cancellationToken)
	{
		var preferenceDocs = await db.FriendAutoSharePreferences
			.Where(p =>
				(p.UserId == requesterUserId && p.FriendUserId == recipientUserId) ||
				(p.UserId == recipientUserId && p.FriendUserId == requesterUserId))
			.ToListAsync(cancellationToken);

		var requesterPreference = preferenceDocs.FirstOrDefault(p =>
			p.UserId == requesterUserId && p.FriendUserId == recipientUserId);
		var recipientPreference = preferenceDocs.FirstOrDefault(p =>
			p.UserId == recipientUserId && p.FriendUserId == requesterUserId);

		var weekStartStr = NormalizeToMonday(DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd");

		if (requesterPreference?.AutoShareMealPlans == true)
			await TryBackfillMealPlanShareAsync(db, requesterUserId, recipientUserId, weekStartStr, cancellationToken);

		if (requesterPreference?.AutoShareGroceryLists == true)
			await TryBackfillGroceryListShareAsync(db, requesterUserId, recipientUserId, weekStartStr, cancellationToken);

		if (recipientPreference?.AutoShareMealPlans == true)
			await TryBackfillMealPlanShareAsync(db, recipientUserId, requesterUserId, weekStartStr, cancellationToken);

		if (recipientPreference?.AutoShareGroceryLists == true)
			await TryBackfillGroceryListShareAsync(db, recipientUserId, requesterUserId, weekStartStr, cancellationToken);
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
