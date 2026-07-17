using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command to generate a grocery list from the family's meal plan for a given week.
/// </summary>
public record GenerateGroceryListCommand(Guid FamilyGroupId, DateOnly WeekStart) : ICommand<GroceryList>;

/// <summary>
/// Generates a grocery list by aggregating ingredients from all recipe-linked meals in the plan.
/// Free-text items (no RecipeId) are added as uncategorized entries.
/// If a grocery list already exists for the same family + week, it is replaced (upsert).
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
				.FirstOrDefaultAsync(
					p => p.FamilyGroupId == command.FamilyGroupId && p.WeekStart == weekStartStr,
					cancellationToken);

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
				.FirstOrDefaultAsync(
					g => g.FamilyGroupId == command.FamilyGroupId && g.WeekStart == weekStartStr,
					cancellationToken);

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
					FamilyGroupId = command.FamilyGroupId,
					WeekStart = weekStartStr,
					Items = groceryItems,
					PantryStapleItems = pantryStapleItems,
					CreatedAt = now,
					UpdatedAt = now
				};
				db.GroceryLists.Add(entity);
			}

			await db.SaveChangesAsync(cancellationToken);

			return Result<GroceryList>.Success(GroceryListHelpers.MapToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<GroceryList>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to generate grocery list.", ex));
		}
	}
}
