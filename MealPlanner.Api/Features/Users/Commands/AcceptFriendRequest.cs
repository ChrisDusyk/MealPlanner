using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Users.Commands;

/// <summary>
/// Command to accept an incoming friend request.
/// </summary>
public record AcceptFriendRequestCommand(
	string RecipientUserId,
	string RequestId
) : ICommand<FriendRequestActionResult>;

public record FriendRequestActionResult(string RequesterUserId);

public class AcceptFriendRequestCommandHandler(IMongoClient mongoClient)
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

		FriendRequestDocument? request = null;

		try
		{
			var database = mongoClient.GetDatabase("mealplannerDb");
			var requests = database.GetCollection<FriendRequestDocument>("friend_requests");
			var friendships = database.GetCollection<FriendshipDocument>("friendships");
			var preferences = database.GetCollection<FriendAutoSharePreferenceDocument>("friend_auto_share_preferences");

			request = await requests
				.Find(r => r.Id == command.RequestId && r.RecipientUserId == command.RecipientUserId)
				.FirstOrDefaultAsync(cancellationToken);
			if (request is null)
			{
				return Result<FriendRequestActionResult>.Failure(
					new Error(ErrorCodes.NotFound, "Friend request was not found."));
			}

			var (userAId, userBId) = SendFriendRequestByEmailCommandHandler.NormalizePair(
				request.RequesterUserId,
				request.RecipientUserId);

			var existingFriendship = await friendships
				.Find(f => f.UserAId == userAId && f.UserBId == userBId)
				.FirstOrDefaultAsync(cancellationToken);

			if (existingFriendship is null)
			{
				await friendships.InsertOneAsync(new FriendshipDocument
				{
					UserAId = userAId,
					UserBId = userBId,
					CreatedAt = DateTime.UtcNow
				}, cancellationToken: cancellationToken);
			}

			await EnsureDefaultPreferenceDocumentAsync(preferences, request.RequesterUserId, request.RecipientUserId,
				cancellationToken);
			await EnsureDefaultPreferenceDocumentAsync(preferences, request.RecipientUserId, request.RequesterUserId,
				cancellationToken);

			await BackfillCurrentWeekSharesAsync(database, preferences, request.RequesterUserId, request.RecipientUserId,
				cancellationToken);

			await requests.DeleteOneAsync(
				r => r.Id == request.Id,
				cancellationToken);

			await requests.DeleteOneAsync(
				r => r.RequesterUserId == request.RecipientUserId && r.RecipientUserId == request.RequesterUserId,
				cancellationToken);

			return Result<FriendRequestActionResult>.Success(new FriendRequestActionResult(request.RequesterUserId));
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
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

	private static async Task EnsureDefaultPreferenceDocumentAsync(
		IMongoCollection<FriendAutoSharePreferenceDocument>? preferences,
		string userId,
		string friendUserId,
		CancellationToken cancellationToken)
	{
		if (preferences is null)
			return;

		var now = DateTime.UtcNow;
		var filter = Builders<FriendAutoSharePreferenceDocument>.Filter.And(
			Builders<FriendAutoSharePreferenceDocument>.Filter.Eq(p => p.UserId, userId),
			Builders<FriendAutoSharePreferenceDocument>.Filter.Eq(p => p.FriendUserId, friendUserId));

		var update = Builders<FriendAutoSharePreferenceDocument>.Update
			.SetOnInsert(p => p.UserId, userId)
			.SetOnInsert(p => p.FriendUserId, friendUserId)
			.SetOnInsert(p => p.AutoShareMealPlans, false)
			.SetOnInsert(p => p.AutoShareGroceryLists, false)
			.SetOnInsert(p => p.CreatedAt, now)
			.SetOnInsert(p => p.UpdatedAt, now);

		await preferences.UpdateOneAsync(
			filter,
			update,
			new UpdateOptions { IsUpsert = true },
			cancellationToken);
	}

	private static async Task BackfillCurrentWeekSharesAsync(
		IMongoDatabase database,
		IMongoCollection<FriendAutoSharePreferenceDocument>? preferences,
		string requesterUserId,
		string recipientUserId,
		CancellationToken cancellationToken)
	{
		if (preferences is null)
			return;

		var preferenceDocs = await preferences
			.Find(p =>
				(p.UserId == requesterUserId && p.FriendUserId == recipientUserId) ||
				(p.UserId == recipientUserId && p.FriendUserId == requesterUserId))
			.ToListAsync(cancellationToken);

		var requesterPreference = preferenceDocs.FirstOrDefault(p =>
			p.UserId == requesterUserId && p.FriendUserId == recipientUserId);
		var recipientPreference = preferenceDocs.FirstOrDefault(p =>
			p.UserId == recipientUserId && p.FriendUserId == requesterUserId);

		var weekStartStr = NormalizeToMonday(DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd");

		if (requesterPreference?.AutoShareMealPlans == true)
		{
			await TryBackfillMealPlanShareAsync(database, requesterUserId, recipientUserId, weekStartStr,
				cancellationToken);
		}

		if (requesterPreference?.AutoShareGroceryLists == true)
		{
			await TryBackfillGroceryListShareAsync(database, requesterUserId, recipientUserId, weekStartStr,
				cancellationToken);
		}

		if (recipientPreference?.AutoShareMealPlans == true)
		{
			await TryBackfillMealPlanShareAsync(database, recipientUserId, requesterUserId, weekStartStr,
				cancellationToken);
		}

		if (recipientPreference?.AutoShareGroceryLists == true)
		{
			await TryBackfillGroceryListShareAsync(database, recipientUserId, requesterUserId, weekStartStr,
				cancellationToken);
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
