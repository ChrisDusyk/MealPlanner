using MealPlanner.Api.Data;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command to toggle the IsChecked state of a grocery list item by index.
/// </summary>
public record ToggleGroceryListItemCommand(
	Guid FamilyGroupId,
	DateOnly WeekStart,
	int ItemIndex
) : ICommand<GroceryList>;

/// <summary>
/// Toggles a single item's checked state in the family's grocery list.
/// </summary>
public class ToggleGroceryListItemCommandHandler(MealPlannerDbContext db)
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

			var entity = await db.GroceryLists
				.FirstOrDefaultAsync(
					g => g.FamilyGroupId == command.FamilyGroupId && g.WeekStart == weekStartStr,
					cancellationToken);

			if (entity is null)
			{
				return Result<GroceryList>.Failure(
					new Error(ErrorCodes.NotFound, "No grocery list found for the specified week."));
			}

			if (command.ItemIndex < 0 || command.ItemIndex >= entity.Items.Count)
			{
				return Result<GroceryList>.Failure(
					new Error(ErrorCodes.ValidationFailed,
						$"Item index {command.ItemIndex} is out of range (0–{entity.Items.Count - 1})."));
			}

			// Toggle the checked state
			entity.Items[command.ItemIndex].IsChecked = !entity.Items[command.ItemIndex].IsChecked;
			entity.UpdatedAt = DateTime.UtcNow;
			await db.SaveChangesAsync(cancellationToken);

			return Result<GroceryList>.Success(GroceryListHelpers.MapToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<GroceryList>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to toggle grocery list item.", ex));
		}
	}
}
