using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command to toggle the IsChecked state of a grocery list item by index.
/// </summary>
public record ToggleGroceryListItemCommand(
	string UserId,
	DateOnly WeekStart,
	int ItemIndex
) : ICommand<GroceryList>;

/// <summary>
/// Toggles a single item's checked state in the grocery list.
/// </summary>
public class ToggleGroceryListItemCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<ToggleGroceryListItemCommand, GroceryList>
{
	public async Task<Result<GroceryList>> HandleAsync(
		ToggleGroceryListItemCommand command,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var weekStartStr = command.WeekStart.ToString("yyyy-MM-dd");
			var collection = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<GroceryListDocument>("grocerylists");

			var document = await collection
				.Find(g => g.UserId == command.UserId && g.WeekStart == weekStartStr)
				.FirstOrDefaultAsync(cancellationToken);

			if (document is null)
			{
				return Result<GroceryList>.Failure(
					new Error(ErrorCodes.NotFound, "No grocery list found for the specified week."));
			}

			if (command.ItemIndex < 0 || command.ItemIndex >= document.Items.Count)
			{
				return Result<GroceryList>.Failure(
					new Error(ErrorCodes.ValidationFailed,
						$"Item index {command.ItemIndex} is out of range (0–{document.Items.Count - 1})."));
			}

			// Toggle the checked state
			document.Items[command.ItemIndex].IsChecked = !document.Items[command.ItemIndex].IsChecked;
			document.UpdatedAt = DateTime.UtcNow;

			var filter = Builders<GroceryListDocument>.Filter.Eq(g => g.Id, document.Id);
			await collection.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);

			return Result<GroceryList>.Success(
				GenerateGroceryListCommandHandler.MapToDomain(document));
		}
		catch (Exception ex)
		{
			return Result<GroceryList>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to toggle grocery list item.", ex));
		}
	}
}
