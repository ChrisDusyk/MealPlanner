using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.MealPlans.Queries;

/// <summary>
/// Query to retrieve a meal plan for a given week. Auto-creates an empty plan if none exists.
/// </summary>
public record GetMealPlanQuery(string UserId, DateOnly WeekStart) : IQuery<MealPlan>;

/// <summary>
/// Handles retrieving (or auto-creating) a weekly meal plan.
/// </summary>
public class GetMealPlanQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetMealPlanQuery, MealPlan>
{
	private static readonly MealCategory[] AllCategories =
		[MealCategory.Breakfast, MealCategory.Lunch, MealCategory.Supper, MealCategory.Snacks];

	private static readonly DayOfWeek[] WeekDays =
		[DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
		 DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

	public async Task<Result<MealPlan>> HandleAsync(
		GetMealPlanQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var weekStart = NormalizeToMonday(query.WeekStart);
			var weekStartStr = weekStart.ToString("yyyy-MM-dd");

			var entity = await db.MealPlans
				.FirstOrDefaultAsync(p => p.UserId == query.UserId && p.WeekStart == weekStartStr, cancellationToken);

			if (entity is not null)
				return Result<MealPlan>.Success(MapToDomain(entity));

			// Auto-create empty plan for this week
			var now = DateTime.UtcNow;
			entity = new MealPlanEntity
			{
				Id = Guid.NewGuid(),
				UserId = query.UserId,
				WeekStart = weekStartStr,
				Days = WeekDays.Select(day => new DayPlanData
				{
					Day = day.ToString(),
					Slots = AllCategories.ToDictionary(
						c => c.ToString(),
						_ => new List<MealSlotItemData>()
					)
				}).ToList(),
				CreatedAt = now,
				UpdatedAt = now
			};

			db.MealPlans.Add(entity);
			await db.SaveChangesAsync(cancellationToken);
			await PropagateAutoSharesFromFriendPreferencesAsync(db, query.UserId, weekStartStr, cancellationToken);
			return Result<MealPlan>.Success(MapToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<MealPlan>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve meal plan.", ex));
		}
	}

	/// <summary>
	/// Normalizes any date to the Monday of the week it falls in.
	/// </summary>
	internal static DateOnly NormalizeToMonday(DateOnly date)
	{
		var dayOfWeek = date.DayOfWeek;
		var offset = dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;
		return date.AddDays(-offset);
	}

	internal static MealPlan MapToDomain(MealPlanEntity entity) =>
		new(
			Id: entity.Id.ToString(),
			UserId: entity.UserId,
			WeekStart: DateOnly.ParseExact(entity.WeekStart, "yyyy-MM-dd"),
			Days: entity.Days.Select(d => new DayPlan(
				Day: Enum.Parse<DayOfWeek>(d.Day, ignoreCase: true),
				Slots: d.Slots.ToDictionary(
					kvp => Enum.Parse<MealCategory>(kvp.Key, ignoreCase: true),
					kvp => kvp.Value.Select(item => new MealSlotItem(
						RecipeId: Option<string>.From(item.RecipeId),
						Name: item.Name,
						Servings: item.Servings
					)).ToList()
				)
			)).ToList(),
			CreatedAt: entity.CreatedAt,
			UpdatedAt: entity.UpdatedAt
		);

	internal static MealPlanShare MapShareToDomain(MealPlanShareEntity entity) =>
		new(
			Id: entity.Id.ToString(),
			OwnerUserId: entity.OwnerUserId,
			SharedWithUserId: entity.SharedWithUserId,
			WeekStart: entity.WeekStart,
			Permission: Enum.Parse<SharePermission>(entity.Permission),
			SharedAt: entity.SharedAt,
			DismissedByRecipient: entity.DismissedByRecipient
		);

	private static async Task PropagateAutoSharesFromFriendPreferencesAsync(
		MealPlannerDbContext db,
		string ownerUserId,
		string weekStart,
		CancellationToken cancellationToken)
	{
		var enabledPreferences = await db.FriendAutoSharePreferences
			.Where(p => p.UserId == ownerUserId && p.AutoShareMealPlans)
			.ToListAsync(cancellationToken);

		if (enabledPreferences.Count == 0)
			return;

		var friendUserIds = enabledPreferences
			.Select(p => p.FriendUserId)
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.Distinct(StringComparer.Ordinal)
			.ToList();

		if (friendUserIds.Count == 0)
			return;

		// Ensure auto-share is only propagated to current friends, not stale preference entries.
		var activeFriendships = await db.Friendships
			.Where(f =>
				(f.UserAId == ownerUserId && friendUserIds.Contains(f.UserBId)) ||
				(f.UserBId == ownerUserId && friendUserIds.Contains(f.UserAId)))
			.ToListAsync(cancellationToken);

		var activeFriendUserIds = activeFriendships
			.Select(f => f.UserAId == ownerUserId ? f.UserBId : f.UserAId)
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.ToHashSet(StringComparer.Ordinal);

		friendUserIds = friendUserIds
			.Where(id => activeFriendUserIds.Contains(id))
			.ToList();

		if (friendUserIds.Count == 0)
			return;

		var existingShares = await db.MealPlanShares
			.Where(s =>
				s.OwnerUserId == ownerUserId &&
				s.WeekStart == weekStart &&
				friendUserIds.Contains(s.SharedWithUserId))
			.ToListAsync(cancellationToken);

		var alreadySharedWith = existingShares
			.Select(s => s.SharedWithUserId)
			.ToHashSet(StringComparer.Ordinal);

		var newShares = friendUserIds
			.Where(friendUserId => !alreadySharedWith.Contains(friendUserId))
			.Select(friendUserId => new MealPlanShareEntity
			{
				Id = Guid.NewGuid(),
				OwnerUserId = ownerUserId,
				SharedWithUserId = friendUserId,
				WeekStart = weekStart,
				Permission = nameof(SharePermission.ReadWrite),
				SharedAt = DateTime.UtcNow,
				DismissedByRecipient = false
			})
			.ToList();

		if (newShares.Count == 0)
			return;

		db.MealPlanShares.AddRange(newShares);
		await db.SaveChangesAsync(cancellationToken);
	}
}
