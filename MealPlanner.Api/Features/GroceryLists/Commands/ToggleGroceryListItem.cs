using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command to toggle the IsChecked state of a grocery list item by index.
/// If OwnerUserId is set and differs from RequestingUserId, the handler verifies
/// the requesting user has a ReadWrite share on the owner's list for that week.
/// </summary>
public record ToggleGroceryListItemCommand(
	string RequestingUserId,
	DateOnly WeekStart,
	int ItemIndex,
	string? OwnerUserId = null
) : ICommand<GroceryList>
{
	/// <summary>The user ID whose list will actually be modified.</summary>
	public string EffectiveOwnerUserId => OwnerUserId ?? RequestingUserId;
}

/// <summary>
/// Toggles a single item's checked state in the grocery list.
/// Supports both owned lists and shared ReadWrite lists.
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
			var weekStartStr = GroceryListHelpers.NormalizeToMonday(command.WeekStart)
				.ToString("yyyy-MM-dd");
			var db = mongoClient.GetDatabase("mealplannerDb");
			var collection = db.GetCollection<GroceryListDocument>("grocerylists");

			// If a different owner is specified, validate the requesting user has ReadWrite access
			if (!string.IsNullOrEmpty(command.OwnerUserId)
			    && command.OwnerUserId != command.RequestingUserId)
			{
				var sharesCollection = db.GetCollection<GroceryListShareDocument>("grocerylist_shares");
				var shareFilter = Builders<GroceryListShareDocument>.Filter.And(
					Builders<GroceryListShareDocument>.Filter.Eq(s => s.OwnerUserId, command.OwnerUserId),
					Builders<GroceryListShareDocument>.Filter.Eq(s => s.SharedWithUserId, command.RequestingUserId),
					Builders<GroceryListShareDocument>.Filter.Eq(s => s.WeekStart, weekStartStr),
					Builders<GroceryListShareDocument>.Filter.Eq(s => s.DismissedByRecipient, false));

				var share = await sharesCollection.Find(shareFilter).FirstOrDefaultAsync(cancellationToken);

				if (share is null)
				{
					return Result<GroceryList>.Failure(
						new Error(ErrorCodes.ValidationFailed,
							"You do not have access to this grocery list."));
				}

				if (!Enum.TryParse<SharePermission>(share.Permission, out var permission)
				    || permission != SharePermission.ReadWrite)
				{
					return Result<GroceryList>.Failure(
						new Error(ErrorCodes.ValidationFailed,
							"You only have read-only access to this grocery list."));
				}
			}

			var document = await collection
				.Find(g => g.UserId == command.EffectiveOwnerUserId && g.WeekStart == weekStartStr)
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

			return Result<GroceryList>.Success(GroceryListHelpers.MapToDomain(document));
		}
		catch (Exception ex)
		{
			return Result<GroceryList>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to toggle grocery list item.", ex));
		}
	}
}
