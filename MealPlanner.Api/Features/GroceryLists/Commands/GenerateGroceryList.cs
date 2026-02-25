using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command to generate a grocery list from a meal plan for a given week.
/// </summary>
public record GenerateGroceryListCommand(string UserId, DateOnly WeekStart) : ICommand<GroceryList>;

/// <summary>
/// Generates a grocery list by aggregating ingredients from all recipe-linked meals in the plan.
/// Free-text items (no RecipeId) are added as uncategorized entries.
/// If a grocery list already exists for the same user + week, it is replaced (upsert).
/// </summary>
public class GenerateGroceryListCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<GenerateGroceryListCommand, GroceryList>
{
	public async Task<Result<GroceryList>> HandleAsync(
		GenerateGroceryListCommand command,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var db = mongoClient.GetDatabase("mealplannerDb");
			var weekStart = GroceryListHelpers.NormalizeToMonday(command.WeekStart);
			var weekStartStr = weekStart.ToString("yyyy-MM-dd");

			// 1. Fetch the meal plan
			var mealPlanCollection = db.GetCollection<MealPlanDocument>("mealplans");
			var mealPlanDoc = await mealPlanCollection
				.Find(p => p.UserId == command.UserId && p.WeekStart == weekStartStr)
				.FirstOrDefaultAsync(cancellationToken);

			if (mealPlanDoc is null)
			{
				return Result<GroceryList>.Failure(
					new Error(ErrorCodes.NotFound, "No meal plan found for the specified week."));
			}

			// 2. Collect all slot items, separating recipe-linked from free-text
			var recipeIds = new HashSet<string>();
			var freeTextItems = new List<string>();

			foreach (var day in mealPlanDoc.Days)
			{
				foreach (var slot in day.Slots.Values)
				{
					foreach (var item in slot)
					{
						if (!string.IsNullOrEmpty(item.RecipeId))
							recipeIds.Add(item.RecipeId);
						else
							freeTextItems.Add(item.Name);
					}
				}
			}

			// 3. Batch-fetch recipes
			var recipesCollection = db.GetCollection<RecipeDocument>("recipes");
			var recipeFilter = Builders<RecipeDocument>.Filter.In(r => r.Id, recipeIds);
			var recipeDocs = await recipesCollection
				.Find(recipeFilter)
				.ToListAsync(cancellationToken);

			var recipeMap = recipeDocs.ToDictionary(r => r.Id!, r => r);

			// 4. Aggregate ingredients by (name, unit) — case-insensitive
			// Key: (lowered name, lowered unit) → (display name, total quantity, display unit, source recipe names)
			var aggregated =
				new Dictionary<(string, string), (string DisplayName, decimal TotalQuantity, string DisplayUnit,
					HashSet<string> Sources)>();

			foreach (var day in mealPlanDoc.Days)
			{
				foreach (var slot in day.Slots.Values)
				{
					foreach (var item in slot)
					{
						if (string.IsNullOrEmpty(item.RecipeId) ||
						    !recipeMap.TryGetValue(item.RecipeId, out var recipe))
							continue;

						foreach (var ingredient in recipe.Ingredients)
						{
							var key = (ingredient.Name.ToLowerInvariant(), ingredient.Unit.ToLowerInvariant());
							if (aggregated.TryGetValue(key, out var existing))
							{
								existing.TotalQuantity += ingredient.Quantity;
								existing.Sources.Add(recipe.Name);
								aggregated[key] = existing;
							}
							else
							{
								aggregated[key] = (ingredient.Name, ingredient.Quantity, ingredient.Unit,
									[recipe.Name]);
							}
						}
					}
				}
			}

			// 5. Build grocery list items
			var groceryItems = aggregated
				.OrderBy(kvp => kvp.Key.Item1)
				.Select(kvp => new GroceryListItemDocument
				{
					Name = kvp.Value.DisplayName,
					Quantity = kvp.Value.TotalQuantity,
					Unit = kvp.Value.DisplayUnit,
					IsChecked = false,
					SourceRecipeNames = kvp.Value.Sources.OrderBy(s => s).ToList()
				})
				.ToList();

			// Add free-text items (deduplicated, case-insensitive)
			var addedFreeText = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var name in freeTextItems)
			{
				if (addedFreeText.Add(name))
				{
					groceryItems.Add(new GroceryListItemDocument
					{
						Name = name,
						Quantity = 0,
						Unit = string.Empty,
						IsChecked = false,
						SourceRecipeNames = []
					});
				}
			}

			// 6. Upsert the grocery list document
			var groceryCollection = db.GetCollection<GroceryListDocument>("grocerylists");
			var now = DateTime.UtcNow;

			var filter = Builders<GroceryListDocument>.Filter.And(
				Builders<GroceryListDocument>.Filter.Eq(g => g.UserId, command.UserId),
				Builders<GroceryListDocument>.Filter.Eq(g => g.WeekStart, weekStartStr));

			var existingDoc = await groceryCollection
				.Find(filter)
				.FirstOrDefaultAsync(cancellationToken);

			GroceryListDocument document;
			if (existingDoc is not null)
			{
				existingDoc.Items = groceryItems;
				existingDoc.UpdatedAt = now;
				await groceryCollection.ReplaceOneAsync(
					filter, existingDoc, cancellationToken: cancellationToken);
				document = existingDoc;
			}
			else
			{
				document = new GroceryListDocument
				{
					UserId = command.UserId,
					WeekStart = weekStartStr,
					Items = groceryItems,
					CreatedAt = now,
					UpdatedAt = now
				};
				await groceryCollection.InsertOneAsync(document, cancellationToken: cancellationToken);
			}

			// 7. Auto-share the grocery list with everyone the meal plan is shared with
			await PropagateSharesFromMealPlanAsync(db, command.UserId, weekStartStr, cancellationToken);

			return Result<GroceryList>.Success(GroceryListHelpers.MapToDomain(document));
		}
		catch (Exception ex)
		{
			return Result<GroceryList>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to generate grocery list.", ex));
		}
	}

	/// <summary>
	/// Upserts grocery list shares for all active meal plan shares that do not already
	/// have a corresponding grocery list share. No-ops if there are no meal plan shares.
	/// </summary>
	private static async Task PropagateSharesFromMealPlanAsync(
		IMongoDatabase db,
		string ownerUserId,
		string weekStartStr,
		CancellationToken cancellationToken)
	{
		var mealPlanSharesCollection = db.GetCollection<MealPlanShareDocument>("shares");
		var groceryListSharesCollection = db.GetCollection<GroceryListShareDocument>("grocerylist_shares");

		// Find all active (non-dismissed) meal plan shares for this owner and week
		var mealShareFilter = Builders<MealPlanShareDocument>.Filter.And(
			Builders<MealPlanShareDocument>.Filter.Eq(s => s.OwnerUserId, ownerUserId),
			Builders<MealPlanShareDocument>.Filter.Eq(s => s.WeekStart, weekStartStr),
			Builders<MealPlanShareDocument>.Filter.Eq(s => s.DismissedByRecipient, false));

		var mealPlanShares = await mealPlanSharesCollection
			.Find(mealShareFilter)
			.ToListAsync(cancellationToken);

		if (mealPlanShares.Count == 0)
			return;

		// Fetch existing grocery list shares so we do not create duplicates
		var recipientIds = mealPlanShares.Select(s => s.SharedWithUserId).Distinct().ToList();
		var existingShareFilter = Builders<GroceryListShareDocument>.Filter.And(
			Builders<GroceryListShareDocument>.Filter.Eq(s => s.OwnerUserId, ownerUserId),
			Builders<GroceryListShareDocument>.Filter.Eq(s => s.WeekStart, weekStartStr),
			Builders<GroceryListShareDocument>.Filter.In(s => s.SharedWithUserId, recipientIds));

		var existingShares = await groceryListSharesCollection
			.Find(existingShareFilter)
			.ToListAsync(cancellationToken);

		var alreadySharedWith = existingShares
			.Select(s => s.SharedWithUserId)
			.ToHashSet();

		var newShares = mealPlanShares
			.Where(s => !alreadySharedWith.Contains(s.SharedWithUserId))
			.Select(s => new GroceryListShareDocument
			{
				OwnerUserId = s.OwnerUserId,
				SharedWithUserId = s.SharedWithUserId,
				WeekStart = s.WeekStart,
				Permission = s.Permission,
				SharedAt = DateTime.UtcNow,
				DismissedByRecipient = false
			})
			.ToList();

		if (newShares.Count > 0)
		{
			try
			{
				await groceryListSharesCollection.InsertManyAsync(newShares, cancellationToken: cancellationToken);
			}
			catch (MongoBulkWriteException<GroceryListShareDocument> ex)
			{
				var hasNonDuplicateErrors = ex.WriteErrors.Any(e =>
					e.Code is not (11000 or 11001 or 12582));

				if (hasNonDuplicateErrors)
					throw;
			}
		}
	}
}
