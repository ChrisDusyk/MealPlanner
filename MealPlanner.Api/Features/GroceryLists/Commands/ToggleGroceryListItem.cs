using MealPlanner.Api.Data;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

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

			// If a different owner is specified, validate the requesting user has ReadWrite access
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
