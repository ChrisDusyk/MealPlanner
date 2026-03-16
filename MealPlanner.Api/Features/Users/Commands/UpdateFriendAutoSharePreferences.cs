using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

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

public class UpdateFriendAutoSharePreferencesCommandHandler(IMongoClient mongoClient)
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
			var database = mongoClient.GetDatabase("mealplannerDb");
			var friendships = database.GetCollection<FriendshipDocument>("friendships");
			var preferences = database.GetCollection<FriendAutoSharePreferenceDocument>("friend_auto_share_preferences");

			var (userAId, userBId) = SendFriendRequestByEmailCommandHandler.NormalizePair(
				command.UserId,
				command.FriendUserId);

			var existingFriendship = await friendships
				.Find(f => f.UserAId == userAId && f.UserBId == userBId)
				.FirstOrDefaultAsync(cancellationToken);

			if (existingFriendship is null)
			{
				return Result<UpdateFriendAutoSharePreferencesResult>.Failure(
					new Error(ErrorCodes.NotFound, "Friendship was not found."));
			}

			var now = DateTime.UtcNow;
			var filter = Builders<FriendAutoSharePreferenceDocument>.Filter.And(
				Builders<FriendAutoSharePreferenceDocument>.Filter.Eq(p => p.UserId, command.UserId),
				Builders<FriendAutoSharePreferenceDocument>.Filter.Eq(p => p.FriendUserId, command.FriendUserId));

			var update = Builders<FriendAutoSharePreferenceDocument>.Update
				.Set(p => p.AutoShareMealPlans, command.AutoShareMealPlans)
				.Set(p => p.AutoShareGroceryLists, command.AutoShareGroceryLists)
				.Set(p => p.UpdatedAt, now)
				.SetOnInsert(p => p.UserId, command.UserId)
				.SetOnInsert(p => p.FriendUserId, command.FriendUserId)
				.SetOnInsert(p => p.CreatedAt, now);

			await preferences.UpdateOneAsync(
				filter,
				update,
				new UpdateOptions { IsUpsert = true },
				cancellationToken);

			var weekStart = NormalizeToMonday(DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd");
			if (command.AutoShareMealPlans)
			{
				await TryBackfillMealPlanShareAsync(
					database,
					command.UserId,
					command.FriendUserId,
					weekStart,
					cancellationToken);
			}

			if (command.AutoShareGroceryLists)
			{
				await TryBackfillGroceryListShareAsync(
					database,
					command.UserId,
					command.FriendUserId,
					weekStart,
					cancellationToken);
			}

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
		IMongoDatabase database,
		string ownerUserId,
		string sharedWithUserId,
		string weekStart,
		CancellationToken cancellationToken)
	{
		var mealPlans = database.GetCollection<MealPlanDocument>("mealplans");
		var shares = database.GetCollection<MealPlanShareDocument>("shares");

		if (mealPlans is null || shares is null)
			return;

		var mealPlan = await mealPlans
			.Find(p => p.UserId == ownerUserId && p.WeekStart == weekStart)
			.FirstOrDefaultAsync(cancellationToken);

		if (mealPlan is null)
			return;

		try
		{
			await shares.InsertOneAsync(new MealPlanShareDocument
			{
				OwnerUserId = ownerUserId,
				SharedWithUserId = sharedWithUserId,
				WeekStart = weekStart,
				Permission = nameof(SharePermission.ReadWrite),
				SharedAt = DateTime.UtcNow,
				DismissedByRecipient = false
			}, cancellationToken: cancellationToken);
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			// Share already exists, no additional action required.
		}
	}

	private static async Task TryBackfillGroceryListShareAsync(
		IMongoDatabase database,
		string ownerUserId,
		string sharedWithUserId,
		string weekStart,
		CancellationToken cancellationToken)
	{
		var groceryLists = database.GetCollection<GroceryListDocument>("grocerylists");
		var shares = database.GetCollection<GroceryListShareDocument>("grocerylist_shares");

		if (groceryLists is null || shares is null)
			return;

		var groceryList = await groceryLists
			.Find(g => g.UserId == ownerUserId && g.WeekStart == weekStart)
			.FirstOrDefaultAsync(cancellationToken);

		if (groceryList is null)
			return;

		try
		{
			await shares.InsertOneAsync(new GroceryListShareDocument
			{
				OwnerUserId = ownerUserId,
				SharedWithUserId = sharedWithUserId,
				WeekStart = weekStart,
				Permission = nameof(SharePermission.ReadWrite),
				SharedAt = DateTime.UtcNow,
				DismissedByRecipient = false
			}, cancellationToken: cancellationToken);
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			// Share already exists, no additional action required.
		}
	}

	private static DateOnly NormalizeToMonday(DateOnly date)
	{
		var dayOfWeek = date.DayOfWeek;
		var offset = dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;
		return date.AddDays(-offset);
	}
}
