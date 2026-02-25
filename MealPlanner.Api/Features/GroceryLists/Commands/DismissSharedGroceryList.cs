using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command for the recipient to dismiss a shared grocery list.
/// </summary>
public record DismissSharedGroceryListCommand(
	string RecipientUserId,
	string ShareId
) : ICommand<Unit>;

/// <summary>
/// Handles dismissing a shared grocery list by setting DismissedByRecipient = true.
/// Only the recipient can dismiss.
/// </summary>
public class DismissSharedGroceryListCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<DismissSharedGroceryListCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		DismissSharedGroceryListCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.ShareId))
			return Result<Unit>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Share ID is required."));

		try
		{
			var collection = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<GroceryListShareDocument>("grocerylist_shares");

			var filter = Builders<GroceryListShareDocument>.Filter.And(
				Builders<GroceryListShareDocument>.Filter.Eq(s => s.Id, command.ShareId),
				Builders<GroceryListShareDocument>.Filter.Eq(s => s.SharedWithUserId, command.RecipientUserId));

			var update = Builders<GroceryListShareDocument>.Update
				.Set(s => s.DismissedByRecipient, true);

			var result = await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

			if (result.MatchedCount == 0)
				return Result<Unit>.Failure(
					new Error(ErrorCodes.NotFound, "Share not found or you are not the recipient."));

			return Result<Unit>.Success(Unit.Value);
		}
		catch (Exception ex)
		{
			return Result<Unit>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to dismiss shared grocery list.", ex));
		}
	}
}
