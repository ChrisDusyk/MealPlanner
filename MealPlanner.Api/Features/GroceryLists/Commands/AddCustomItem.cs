using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

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

			if (!string.IsNullOrEmpty(command.OwnerUserId)
			    && command.OwnerUserId != command.RequestingUserId)
			{
				var share = await db.GroceryListShares.FirstOrDefaultAsync(
					s => s.OwnerUserId == command.OwnerUserId
						&& s.SharedWithUserId == command.RequestingUserId
						&& s.WeekStart == weekStartStr
						&& !s.DismissedByRecipient,
					cancellationToken);
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

			var entity = await db.GroceryLists
				.FirstOrDefaultAsync(g => g.UserId == command.EffectiveOwnerUserId && g.WeekStart == weekStartStr, cancellationToken);

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
