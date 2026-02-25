using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command to share a grocery list with another user by email.
/// </summary>
public record ShareGroceryListCommand(
	string OwnerUserId,
	string SharedWithEmail,
	string WeekStart,
	SharePermission Permission
) : ICommand<GroceryListShare>;

/// <summary>
/// Handles creating a grocery list share: validates the recipient, prevents duplicates
/// and self-shares, then inserts a share document.
/// </summary>
public class ShareGroceryListCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<ShareGroceryListCommand, GroceryListShare>
{
	public async Task<Result<GroceryListShare>> HandleAsync(
		ShareGroceryListCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.SharedWithEmail))
			return Result<GroceryListShare>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipient email is required."));

		if (string.IsNullOrWhiteSpace(command.WeekStart))
			return Result<GroceryListShare>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Week start date is required."));

		try
		{
			var db = mongoClient.GetDatabase("mealplannerDb");
			var usersCollection = db.GetCollection<UserDocument>("users");
			var groceryListsCollection = db.GetCollection<GroceryListDocument>("grocerylists");
			var sharesCollection = db.GetCollection<GroceryListShareDocument>("grocerylist_shares");

			await EnsureIndexesAsync(sharesCollection, cancellationToken);

			// Look up the owner to prevent self-share
			var ownerFilter = Builders<UserDocument>.Filter.Eq(u => u.Auth0UserId, command.OwnerUserId);
			var owner = await usersCollection.Find(ownerFilter).FirstOrDefaultAsync(cancellationToken);
			if (owner is null)
				return Result<GroceryListShare>.Failure(
					new Error(ErrorCodes.NotFound, "Owner user not found."));

			// Find recipient by email (case-insensitive)
			var emailFilter = Builders<UserDocument>.Filter.Regex(
				u => u.Email,
				new MongoDB.Bson.BsonRegularExpression(
					$"^{System.Text.RegularExpressions.Regex.Escape(command.SharedWithEmail)}$", "i"));

			var recipient = await usersCollection.Find(emailFilter).FirstOrDefaultAsync(cancellationToken);
			if (recipient is null)
				return Result<GroceryListShare>.Failure(
					new Error(ErrorCodes.NotFound, $"No user found with email '{command.SharedWithEmail}'."));

			// Prevent self-share
			if (recipient.Auth0UserId == command.OwnerUserId)
				return Result<GroceryListShare>.Failure(
					new Error(ErrorCodes.ValidationFailed, "You cannot share a grocery list with yourself."));

			// Ensure a grocery list exists for this owner/week before sharing
			var groceryListFilter = Builders<GroceryListDocument>.Filter.And(
				Builders<GroceryListDocument>.Filter.Eq(g => g.UserId, command.OwnerUserId),
				Builders<GroceryListDocument>.Filter.Eq(g => g.WeekStart, command.WeekStart));
			var groceryList =
				await groceryListsCollection.Find(groceryListFilter).FirstOrDefaultAsync(cancellationToken);
			if (groceryList is null)
				return Result<GroceryListShare>.Failure(
					new Error(ErrorCodes.NotFound, "No grocery list exists for the specified week."));

			// Check for existing share
			var existingFilter = Builders<GroceryListShareDocument>.Filter.And(
				Builders<GroceryListShareDocument>.Filter.Eq(s => s.OwnerUserId, command.OwnerUserId),
				Builders<GroceryListShareDocument>.Filter.Eq(s => s.SharedWithUserId, recipient.Auth0UserId),
				Builders<GroceryListShareDocument>.Filter.Eq(s => s.WeekStart, command.WeekStart));

			var existing = await sharesCollection.Find(existingFilter).FirstOrDefaultAsync(cancellationToken);
			if (existing is not null)
				return Result<GroceryListShare>.Failure(
					new Error(ErrorCodes.ValidationFailed, "This grocery list is already shared with that user."));

			// Create the share
			var document = new GroceryListShareDocument
			{
				OwnerUserId = command.OwnerUserId,
				SharedWithUserId = recipient.Auth0UserId,
				WeekStart = command.WeekStart,
				Permission = command.Permission.ToString(),
				SharedAt = DateTime.UtcNow,
				DismissedByRecipient = false
			};

			await sharesCollection.InsertOneAsync(document, cancellationToken: cancellationToken);

			var share = document.ToDomain() with
			{
				SharedWithName = recipient.Name,
				SharedWithEmail = recipient.Email ?? string.Empty
			};

			return Result<GroceryListShare>.Success(share);
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			return Result<GroceryListShare>.Failure(
				new Error(ErrorCodes.ValidationFailed, "This grocery list is already shared with that user."));
		}
		catch (Exception ex)
		{
			return Result<GroceryListShare>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to share grocery list.", ex));
		}
	}

	private static async Task EnsureIndexesAsync(
		IMongoCollection<GroceryListShareDocument> collection,
		CancellationToken cancellationToken)
	{
		try
		{
			var indexModel = new CreateIndexModel<GroceryListShareDocument>(
				Builders<GroceryListShareDocument>.IndexKeys
					.Ascending(s => s.OwnerUserId)
					.Ascending(s => s.SharedWithUserId)
					.Ascending(s => s.WeekStart),
				new CreateIndexOptions { Unique = true, Name = "ux_grocerylist_shares_owner_recipient_week" });

			await collection.Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
		}
		catch (MongoCommandException ex) when (IsIndexConflict(ex))
		{
			// Index already exists with compatible options
		}
	}

	private static bool IsIndexConflict(MongoCommandException ex)
	{
		if (ex.CodeName is "IndexOptionsConflict" or "IndexKeySpecsConflict")
			return true;

		return ex.Result is not null
		       && ex.Result.TryGetValue("codeName", out var value)
		       && value.IsString
		       && value.AsString is "IndexOptionsConflict" or "IndexKeySpecsConflict";
	}
}
