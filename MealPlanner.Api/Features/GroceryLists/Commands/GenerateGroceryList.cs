using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

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
public class GenerateGroceryListCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<GenerateGroceryListCommand, GroceryList>
{
	public async Task<Result<GroceryList>> HandleAsync(
		GenerateGroceryListCommand command,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var weekStart = GroceryListHelpers.NormalizeToMonday(command.WeekStart);
			var weekStartStr = weekStart.ToString("yyyy-MM-dd");

			// 1. Fetch the meal plan
			var mealPlanEntity = await db.MealPlans
				.FirstOrDefaultAsync(p => p.UserId == command.UserId && p.WeekStart == weekStartStr, cancellationToken);

			if (mealPlanEntity is null)
			{
				return Result<GroceryList>.Failure(
					new Error(ErrorCodes.NotFound, "No meal plan found for the specified week."));
			}

			// 2. Collect all slot items, separating recipe-linked from free-text
			var recipeIds = new HashSet<Guid>();
			var freeTextItems = new List<string>();

			foreach (var day in mealPlanEntity.Days)
			{
				foreach (var slot in day.Slots.Values)
				{
					foreach (var item in slot)
					{
						if (!string.IsNullOrEmpty(item.RecipeId) && Guid.TryParse(item.RecipeId, out var recipeId))
							recipeIds.Add(recipeId);
						else
							freeTextItems.Add(item.Name);
					}
				}
			}

			// 3. Batch-fetch recipes
			var recipeEntities = await db.Recipes
				.Where(r => recipeIds.Contains(r.Id))
				.ToListAsync(cancellationToken);

			var recipeMap = recipeEntities.ToDictionary(r => r.Id, r => r);

			// 4. Aggregate ingredients by (name, unit) — case-insensitive
			// Key: (lowered name, lowered unit) → (display name, total quantity, display unit, source recipe names)
			var aggregated =
				new Dictionary<(string, string), (string DisplayName, decimal TotalQuantity, string DisplayUnit,
					HashSet<string> Sources)>();

			var aggregatedStaples =
				new Dictionary<(string, string), (string DisplayName, decimal TotalQuantity, string DisplayUnit,
					HashSet<string> Sources)>();

			foreach (var day in mealPlanEntity.Days)
			{
				foreach (var slot in day.Slots.Values)
				{
					foreach (var item in slot)
					{
						if (string.IsNullOrEmpty(item.RecipeId)
						    || !Guid.TryParse(item.RecipeId, out var recipeId)
						    || !recipeMap.TryGetValue(recipeId, out var recipe))
							continue;

						// Scale ingredient quantities by slot servings vs recipe yield
						var recipeServings = recipe.Servings > 0 ? recipe.Servings : 1;
						var scalingFactor = (decimal)item.Servings / recipeServings;

						foreach (var ingredient in recipe.Ingredients)
						{
							var target = ingredient.IsPantryStaple ? aggregatedStaples : aggregated;
							var key = (ingredient.Name.ToLowerInvariant(), ingredient.Unit.ToLowerInvariant());
							var scaledQuantity = ingredient.Quantity * scalingFactor;
							if (target.TryGetValue(key, out var existing))
							{
								existing.TotalQuantity += scaledQuantity;
								existing.Sources.Add(recipe.Name);
								target[key] = existing;
							}
							else
							{
								target[key] = (ingredient.Name, scaledQuantity, ingredient.Unit,
									[recipe.Name]);
							}
						}
					}
				}
			}

			// 5. Build grocery list items
			var groceryItems = aggregated
				.OrderBy(kvp => kvp.Key.Item1)
				.Select(kvp => new GroceryListItemData
				{
					Name = kvp.Value.DisplayName,
					Quantity = kvp.Value.TotalQuantity,
					Unit = kvp.Value.DisplayUnit,
					IsChecked = false,
					SourceRecipeNames = kvp.Value.Sources.OrderBy(s => s).ToList()
				})
				.ToList();

			// Build pantry staple items
			var pantryStapleItems = aggregatedStaples
				.OrderBy(kvp => kvp.Key.Item1)
				.Select(kvp => new GroceryListItemData
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
					groceryItems.Add(new GroceryListItemData
					{
						Name = name,
						Quantity = 0,
						Unit = string.Empty,
						IsChecked = false,
						SourceRecipeNames = []
					});
				}
			}

			// 6. Upsert the grocery list entity
			var now = DateTime.UtcNow;

			var entity = await db.GroceryLists
				.FirstOrDefaultAsync(g => g.UserId == command.UserId && g.WeekStart == weekStartStr, cancellationToken);

			if (entity is not null)
			{
				entity.Items = groceryItems;
				entity.PantryStapleItems = pantryStapleItems;
				entity.UpdatedAt = now;
			}
			else
			{
				entity = new GroceryListEntity
				{
					Id = Guid.NewGuid(),
					UserId = command.UserId,
					WeekStart = weekStartStr,
					Items = groceryItems,
					PantryStapleItems = pantryStapleItems,
					CreatedAt = now,
					UpdatedAt = now
				};
				db.GroceryLists.Add(entity);
			}

			await db.SaveChangesAsync(cancellationToken);

			// 7. Auto-share the grocery list with everyone the meal plan is shared with
			await PropagateSharesFromMealPlanAsync(db, command.UserId, weekStartStr, cancellationToken);
			await PropagateAutoSharesFromFriendPreferencesAsync(db, command.UserId, weekStartStr, cancellationToken);

			return Result<GroceryList>.Success(GroceryListHelpers.MapToDomain(entity));
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
		MealPlannerDbContext db,
		string ownerUserId,
		string weekStartStr,
		CancellationToken cancellationToken)
	{
		// Find all active (non-dismissed) meal plan shares for this owner and week
		var mealPlanShares = await db.MealPlanShares
			.Where(s => s.OwnerUserId == ownerUserId
				&& s.WeekStart == weekStartStr
				&& !s.DismissedByRecipient)
			.ToListAsync(cancellationToken);

		if (mealPlanShares.Count == 0)
			return;

		// Fetch existing grocery list shares so we do not create duplicates
		var recipientIds = mealPlanShares.Select(s => s.SharedWithUserId).Distinct().ToList();
		var existingShares = await db.GroceryListShares
			.Where(s => s.OwnerUserId == ownerUserId
				&& s.WeekStart == weekStartStr
				&& recipientIds.Contains(s.SharedWithUserId))
			.ToListAsync(cancellationToken);

		var alreadySharedWith = existingShares
			.Select(s => s.SharedWithUserId)
			.ToHashSet();

		var newShares = mealPlanShares
			.Where(s => !alreadySharedWith.Contains(s.SharedWithUserId))
			.Select(s => new GroceryListShareEntity
			{
				Id = Guid.NewGuid(),
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
			db.GroceryListShares.AddRange(newShares);
			await db.SaveChangesAsync(cancellationToken);
		}
	}

	private static async Task PropagateAutoSharesFromFriendPreferencesAsync(
		MealPlannerDbContext db,
		string ownerUserId,
		string weekStartStr,
		CancellationToken cancellationToken)
	{
		var enabledPreferences = await db.FriendAutoSharePreferences
			.Where(p => p.UserId == ownerUserId && p.AutoShareGroceryLists)
			.ToListAsync(cancellationToken);

		if (enabledPreferences.Count == 0)
			return;

		var recipientIds = enabledPreferences
			.Select(p => p.FriendUserId)
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.Distinct(StringComparer.Ordinal)
			.ToList();

		if (recipientIds.Count == 0)
			return;

		var activeFriendships = await db.Friendships
			.Where(f =>
				(f.UserAId == ownerUserId && recipientIds.Contains(f.UserBId)) ||
				(f.UserBId == ownerUserId && recipientIds.Contains(f.UserAId)))
			.ToListAsync(cancellationToken);

		var activeRecipientIds = activeFriendships
			.Select(f => f.UserAId == ownerUserId ? f.UserBId : f.UserAId)
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.Distinct(StringComparer.Ordinal)
			.ToList();

		if (activeRecipientIds.Count == 0)
			return;

		var existingShares = await db.GroceryListShares
			.Where(s =>
				s.OwnerUserId == ownerUserId &&
				s.WeekStart == weekStartStr &&
				activeRecipientIds.Contains(s.SharedWithUserId))
			.ToListAsync(cancellationToken);

		var alreadySharedWith = existingShares
			.Select(s => s.SharedWithUserId)
			.ToHashSet(StringComparer.Ordinal);

		var now = DateTime.UtcNow;
		var newShares = activeRecipientIds
			.Where(recipientId => !alreadySharedWith.Contains(recipientId))
			.Select(recipientId => new GroceryListShareEntity
			{
				Id = Guid.NewGuid(),
				OwnerUserId = ownerUserId,
				SharedWithUserId = recipientId,
				WeekStart = weekStartStr,
				Permission = nameof(SharePermission.ReadWrite),
				SharedAt = now,
				DismissedByRecipient = false
			})
			.ToList();

		if (newShares.Count == 0)
			return;

		db.GroceryListShares.AddRange(newShares);
		await db.SaveChangesAsync(cancellationToken);
	}
}
