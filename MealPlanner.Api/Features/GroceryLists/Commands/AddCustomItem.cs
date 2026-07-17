using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command to add a custom item to the family's grocery list for a week.
/// </summary>
public record AddCustomItemCommand(
	Guid FamilyGroupId,
	DateOnly WeekStart,
	string ItemName
) : ICommand<GroceryList>;

/// <summary>
/// Adds a user-provided custom item to the grocery list.
/// Custom items have quantity 0, no unit, and no source recipes.
/// </summary>
public class AddCustomItemCommandHandler(MealPlannerDbContext db)
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

			var entity = await db.GroceryLists
				.FirstOrDefaultAsync(
					g => g.FamilyGroupId == command.FamilyGroupId && g.WeekStart == weekStartStr,
					cancellationToken);

			if (entity is null)
			{
				return Result<GroceryList>.Failure(
					new Error(ErrorCodes.NotFound,
						"No grocery list found for the specified week. Generate a list first."));
			}

			entity.Items.Add(new GroceryListItemData
			{
				Name = command.ItemName.Trim(),
				Quantity = 0,
				Unit = string.Empty,
				IsChecked = false,
				SourceRecipeNames = []
			});
			entity.UpdatedAt = DateTime.UtcNow;
			await db.SaveChangesAsync(cancellationToken);

			return Result<GroceryList>.Success(
				GroceryListHelpers.MapToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<GroceryList>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to add custom item.", ex));
		}
	}
}
