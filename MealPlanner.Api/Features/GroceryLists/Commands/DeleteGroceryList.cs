using MealPlanner.Api.Data;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.GroceryLists.Commands;

/// <summary>
/// Command to delete a grocery list for a given user and week.
/// </summary>
public record DeleteGroceryListCommand(Guid FamilyGroupId, DateOnly WeekStart) : ICommand<Unit>;

/// <summary>
/// Deletes the grocery list for the specified user and week.
/// </summary>
public class DeleteGroceryListCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<DeleteGroceryListCommand, Unit>
{
	public async Task<Result<Unit>> HandleAsync(
		DeleteGroceryListCommand command,
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
				return Result<Unit>.Failure(
					new Error(ErrorCodes.NotFound, "No grocery list found for the specified week."));
			}

			db.GroceryLists.Remove(entity);
			await db.SaveChangesAsync(cancellationToken);

			return Result<Unit>.Success(new Unit());
		}
		catch (Exception ex)
		{
			return Result<Unit>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to delete grocery list.", ex));
		}
	}
}
