using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command for the owner to revoke a grocery list share.
/// </summary>
public record RevokeGroceryListShareCommand(
	string OwnerUserId,
	string ShareId
) : ICommand<Unit>;

/// <summary>
/// Handles revoking (deleting) a grocery list share. Only the share owner can revoke.
/// </summary>
public class RevokeGroceryListShareCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<RevokeGroceryListShareCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		RevokeGroceryListShareCommand command,
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
				Builders<GroceryListShareDocument>.Filter.Eq(s => s.OwnerUserId, command.OwnerUserId));

			var result = await collection.DeleteOneAsync(filter, cancellationToken);

			if (result.DeletedCount == 0)
				return Result<Unit>.Failure(
					new Error(ErrorCodes.NotFound, "Share not found or you are not the owner."));

			return Result<Unit>.Success(Unit.Value);
		}
		catch (Exception ex)
		{
			return Result<Unit>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to revoke share.", ex));
		}
	}
}
