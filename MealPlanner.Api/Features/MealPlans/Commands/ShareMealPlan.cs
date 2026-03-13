using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.MealPlans.Commands;

/// <summary>
/// Command to share a meal plan with another user by email.
/// </summary>
public record ShareMealPlanCommand(
	string OwnerUserId,
	string SharedWithEmail,
	string WeekStart,
	SharePermission Permission
) : ICommand<MealPlanShare>;

/// <summary>
/// Handles creating a meal plan share: validates the recipient, prevents duplicates
/// and self-shares, then inserts a share document.
/// </summary>
public class ShareMealPlanCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<ShareMealPlanCommand, MealPlanShare>
{
	public async Task<Result<MealPlanShare>> HandleAsync(
		ShareMealPlanCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.SharedWithEmail))
			return Result<MealPlanShare>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipient email is required."));

		if (string.IsNullOrWhiteSpace(command.WeekStart))
			return Result<MealPlanShare>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Week start date is required."));

		try
		{
			var db = mongoClient.GetDatabase("mealplannerDb");
			var usersCollection = db.GetCollection<UserDocument>("users");
			var sharesCollection = db.GetCollection<MealPlanShareDocument>("shares");

			// Look up the owner to get their Auth0UserId for self-share check
			var ownerFilter = Builders<UserDocument>.Filter.Eq(u => u.Auth0UserId, command.OwnerUserId);
			var owner = await usersCollection.Find(ownerFilter).FirstOrDefaultAsync(cancellationToken);
			if (owner is null)
				return Result<MealPlanShare>.Failure(
					new Error(ErrorCodes.NotFound, "Owner user not found."));

			// Find recipient by email (case-insensitive)
			var emailFilter = Builders<UserDocument>.Filter.Regex(
				u => u.Email,
				new MongoDB.Bson.BsonRegularExpression(
					$"^{System.Text.RegularExpressions.Regex.Escape(command.SharedWithEmail)}$", "i"));

			var recipient = await usersCollection.Find(emailFilter).FirstOrDefaultAsync(cancellationToken);
			if (recipient is null)
				return Result<MealPlanShare>.Failure(
					new Error(ErrorCodes.NotFound, $"No user found with email '{command.SharedWithEmail}'."));

			// Prevent self-share
			if (recipient.Auth0UserId == command.OwnerUserId)
				return Result<MealPlanShare>.Failure(
					new Error(ErrorCodes.ValidationFailed, "You cannot share a meal plan with yourself."));

			// Check for existing share
			var existingFilter = Builders<MealPlanShareDocument>.Filter.And(
				Builders<MealPlanShareDocument>.Filter.Eq(s => s.OwnerUserId, command.OwnerUserId),
				Builders<MealPlanShareDocument>.Filter.Eq(s => s.SharedWithUserId, recipient.Auth0UserId),
				Builders<MealPlanShareDocument>.Filter.Eq(s => s.WeekStart, command.WeekStart));

			var existing = await sharesCollection.Find(existingFilter).FirstOrDefaultAsync(cancellationToken);
			if (existing is not null)
				return Result<MealPlanShare>.Failure(
					new Error(ErrorCodes.ValidationFailed, "This meal plan is already shared with that user."));

			// Create the share
			var document = new MealPlanShareDocument
			{
				OwnerUserId = command.OwnerUserId,
				SharedWithUserId = recipient.Auth0UserId,
				WeekStart = command.WeekStart,
				Permission = command.Permission.ToString(),
				SharedAt = DateTime.UtcNow,
				DismissedByRecipient = false
			};

			await sharesCollection.InsertOneAsync(document, cancellationToken: cancellationToken);

			return Result<MealPlanShare>.Success(document.ToDomain());
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			return Result<MealPlanShare>.Failure(
				new Error(ErrorCodes.ValidationFailed, "This meal plan is already shared with that user."));
		}
		catch (Exception ex)
		{
			return Result<MealPlanShare>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to share meal plan.", ex));
		}
	}

}
