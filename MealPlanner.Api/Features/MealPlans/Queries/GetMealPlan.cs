using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.MealPlans.Queries;

/// <summary>
/// Query to retrieve a meal plan for a given week. Auto-creates an empty plan if none exists.
/// </summary>
public record GetMealPlanQuery(string UserId, DateOnly WeekStart) : IQuery<MealPlan>;

/// <summary>
/// Handles retrieving (or auto-creating) a weekly meal plan from MongoDB.
/// </summary>
public class GetMealPlanQueryHandler(IMongoClient mongoClient)
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

			var database = mongoClient.GetDatabase("mealplannerDb");
			var collection = database.GetCollection<MealPlanDocument>("mealplans");

			var document = await collection
				.Find(p => p.UserId == query.UserId && p.WeekStart == weekStartStr)
				.FirstOrDefaultAsync(cancellationToken);

			if (document is not null)
				return Result<MealPlan>.Success(MapToDomain(document));

			// Auto-create empty plan for this week
			var now = DateTime.UtcNow;
			document = new MealPlanDocument
			{
				UserId = query.UserId,
				WeekStart = weekStartStr,
				Days = WeekDays.Select(day => new DayPlanDocument
				{
					Day = day.ToString(),
					Slots = AllCategories.ToDictionary(
						c => c.ToString(),
						_ => new List<MealSlotItemDocument>()
					)
				}).ToList(),
				CreatedAt = now,
				UpdatedAt = now
			};

			await collection.InsertOneAsync(document, cancellationToken: cancellationToken);
			await PropagateAutoSharesFromFriendPreferencesAsync(database, query.UserId, weekStartStr, cancellationToken);
			return Result<MealPlan>.Success(MapToDomain(document));
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

	internal static MealPlan MapToDomain(MealPlanDocument doc) =>
		new(
			Id: doc.Id!,
			UserId: doc.UserId,
			WeekStart: DateOnly.ParseExact(doc.WeekStart, "yyyy-MM-dd"),
			Days: doc.Days.Select(d => new DayPlan(
				Day: Enum.Parse<DayOfWeek>(d.Day),
				Slots: d.Slots.ToDictionary(
					kvp => Enum.Parse<MealCategory>(kvp.Key),
					kvp => kvp.Value.Select(item => new MealSlotItem(
						RecipeId: Option<string>.From(item.RecipeId),
						Name: item.Name,
						Servings: item.Servings
					)).ToList()
				)
			)).ToList(),
			CreatedAt: doc.CreatedAt,
			UpdatedAt: doc.UpdatedAt
		);

	private static async Task PropagateAutoSharesFromFriendPreferencesAsync(
		IMongoDatabase database,
		string ownerUserId,
		string weekStart,
		CancellationToken cancellationToken)
	{
		var preferences = database.GetCollection<FriendAutoSharePreferenceDocument>("friend_auto_share_preferences");
		var shares = database.GetCollection<MealPlanShareDocument>("shares");
		var friendships = database.GetCollection<FriendshipDocument>("friendships");

		if (preferences is null || shares is null || friendships is null)
			return;

		var enabledPreferences = await preferences
			.Find(p => p.UserId == ownerUserId && p.AutoShareMealPlans)
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
		var activeFriendships = await friendships
			.Find(f =>
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

		var existingShares = await shares
			.Find(s =>
				s.OwnerUserId == ownerUserId &&
				s.WeekStart == weekStart &&
				friendUserIds.Contains(s.SharedWithUserId))
			.ToListAsync(cancellationToken);

		var alreadySharedWith = existingShares
			.Select(s => s.SharedWithUserId)
			.ToHashSet(StringComparer.Ordinal);

		var newShares = friendUserIds
			.Where(friendUserId => !alreadySharedWith.Contains(friendUserId))
			.Select(friendUserId => new MealPlanShareDocument
			{
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

		try
		{
			await shares.InsertManyAsync(newShares, cancellationToken: cancellationToken);
		}
		catch (MongoBulkWriteException<MealPlanShareDocument> ex)
		{
			var hasNonDuplicateErrors = ex.WriteErrors.Any(e => e.Code is not (11000 or 11001 or 12582));
			if (hasNonDuplicateErrors)
				throw;
		}
	}
}
