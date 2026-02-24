using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command to delete a grocery list for a given user and week.
/// </summary>
public record DeleteGroceryListCommand(string UserId, DateOnly WeekStart) : ICommand<Unit>;

/// <summary>
/// Deletes the grocery list document from MongoDB.
/// </summary>
public class DeleteGroceryListCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<DeleteGroceryListCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		DeleteGroceryListCommand command,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var weekStartStr = GenerateGroceryListCommandHandler.NormalizeToMonday(command.WeekStart)
				.ToString("yyyy-MM-dd");
			var collection = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<GroceryListDocument>("grocerylists");

			var result = await collection.DeleteOneAsync(
				g => g.UserId == command.UserId && g.WeekStart == weekStartStr,
				cancellationToken);

			if (result.DeletedCount == 0)
			{
				return Result<Unit>.Failure(
					new Error(ErrorCodes.NotFound, "No grocery list found for the specified week."));
			}

			return Result<Unit>.Success(new Unit());
		}
		catch (Exception ex)
		{
			return Result<Unit>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to delete grocery list.", ex));
		}
	}
}
