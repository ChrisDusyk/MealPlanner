using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command to promote a pantry staple item into the main grocery list.
/// Moves the item from PantryStapleItems to Items at the specified index.
/// </summary>
public record PromotePantryStapleItemCommand(
	string UserId,
	DateOnly WeekStart,
	int ItemIndex
) : ICommand<GroceryList>;

/// <summary>
/// Moves a pantry staple item from the review section into the main grocery list.
/// </summary>
public class PromotePantryStapleItemCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<PromotePantryStapleItemCommand, GroceryList>
{
	public async Task<Result<GroceryList>> HandleAsync(
		PromotePantryStapleItemCommand command,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var weekStartStr = GroceryListHelpers.NormalizeToMonday(command.WeekStart)
				.ToString("yyyy-MM-dd");
			var db = mongoClient.GetDatabase("mealplannerDb");
			var collection = db.GetCollection<GroceryListDocument>("grocerylists");

			var document = await collection
				.Find(g => g.UserId == command.UserId && g.WeekStart == weekStartStr)
				.FirstOrDefaultAsync(cancellationToken);

			if (document is null)
			{
				return Result<GroceryList>.Failure(
					new Error(ErrorCodes.NotFound, "No grocery list found for the specified week."));
			}

			if (command.ItemIndex < 0 || command.ItemIndex >= document.PantryStapleItems.Count)
			{
				var errorMessage = document.PantryStapleItems.Count == 0
					? "No pantry staple items are available to promote."
					: $"Item index {command.ItemIndex} is out of range (0–{document.PantryStapleItems.Count - 1}).";

				return Result<GroceryList>.Failure(
					new Error(ErrorCodes.ValidationFailed, errorMessage));
			}

			// Move the item from pantry staples to the main list
			var item = document.PantryStapleItems[command.ItemIndex];
			item.IsChecked = false;
			document.PantryStapleItems.RemoveAt(command.ItemIndex);
			document.Items.Add(item);
			document.UpdatedAt = DateTime.UtcNow;

			var filter = Builders<GroceryListDocument>.Filter.Eq(g => g.Id, document.Id);
			await collection.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);

			return Result<GroceryList>.Success(GroceryListHelpers.MapToDomain(document));
		}
		catch (Exception ex)
		{
			return Result<GroceryList>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to promote pantry staple item.", ex));
		}
	}
}
