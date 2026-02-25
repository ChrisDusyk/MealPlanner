using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command to add a custom item to an existing grocery list.
/// </summary>
public record AddCustomItemCommand(
	string RequestingUserId,
	DateOnly WeekStart,
	string ItemName,
	string? OwnerUserId = null
) : ICommand<GroceryList>
{
	public string EffectiveOwnerUserId => OwnerUserId ?? RequestingUserId;
}

/// <summary>
/// Adds a user-provided custom item to the grocery list.
/// Custom items have quantity 0, no unit, and no source recipes.
/// </summary>
public class AddCustomItemCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<AddCustomItemCommand, GroceryList>
{
	public async Task<Result<GroceryList>> HandleAsync(
		AddCustomItemCommand command,
		CancellationToken cancellationToken = default)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(command.ItemName))
			{
				return Result<GroceryList>.Failure(
					new Error(ErrorCodes.ValidationFailed, "Item name cannot be empty."));
			}

			var weekStartStr = GroceryListHelpers.NormalizeToMonday(command.WeekStart).ToString("yyyy-MM-dd");
			var db = mongoClient.GetDatabase("mealplannerDb");
			var collection = db.GetCollection<GroceryListDocument>("grocerylists");

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

				if (!Enum.TryParse<SharePermission>(share.Permission, true, out var permission)
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
					new Error(ErrorCodes.NotFound,
						"No grocery list found for the specified week. Generate a list first."));
			}

			document.Items.Add(new GroceryListItemDocument
			{
				Name = command.ItemName.Trim(),
				Quantity = 0,
				Unit = string.Empty,
				IsChecked = false,
				SourceRecipeNames = []
			});
			document.UpdatedAt = DateTime.UtcNow;

			var filter = Builders<GroceryListDocument>.Filter.Eq(g => g.Id, document.Id);
			await collection.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);

			return Result<GroceryList>.Success(
				GroceryListHelpers.MapToDomain(document));
		}
		catch (Exception ex)
		{
			return Result<GroceryList>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to add custom item.", ex));
		}
	}
}
