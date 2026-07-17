using MealPlanner.Api.Data;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command to promote a pantry staple item into the main grocery list.
/// Moves the item from PantryStapleItems to Items at the specified index.
/// </summary>
public record PromotePantryStapleItemCommand(
	Guid FamilyGroupId,
	DateOnly WeekStart,
	int ItemIndex
) : ICommand<GroceryList>;

/// <summary>
/// Moves a pantry staple item from the review section into the main grocery list.
/// </summary>
public class PromotePantryStapleItemCommandHandler(MealPlannerDbContext db)
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
			var entity = await db.GroceryLists
				.FirstOrDefaultAsync(g => g.FamilyGroupId == command.FamilyGroupId && g.WeekStart == weekStartStr, cancellationToken);

			if (entity is null)
			{
				return Result<GroceryList>.Failure(
					new Error(ErrorCodes.NotFound, "No grocery list found for the specified week."));
			}

			if (command.ItemIndex < 0 || command.ItemIndex >= entity.PantryStapleItems.Count)
			{
				var errorMessage = entity.PantryStapleItems.Count == 0
					? "No pantry staple items are available to promote."
					: $"Item index {command.ItemIndex} is out of range (0–{entity.PantryStapleItems.Count - 1}).";

				return Result<GroceryList>.Failure(
					new Error(ErrorCodes.ValidationFailed, errorMessage));
			}

			// Move the item from pantry staples to the main list
			var item = entity.PantryStapleItems[command.ItemIndex];
			item.IsChecked = false;
			entity.PantryStapleItems.RemoveAt(command.ItemIndex);
			entity.Items.Add(item);
			entity.UpdatedAt = DateTime.UtcNow;
			await db.SaveChangesAsync(cancellationToken);

			return Result<GroceryList>.Success(GroceryListHelpers.MapToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<GroceryList>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to promote pantry staple item.", ex));
		}
	}
}
