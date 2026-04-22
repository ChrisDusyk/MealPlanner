using MealPlanner.Api.Data;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.GroceryLists.Queries;

/// <summary>
/// Query to retrieve an existing grocery list for a given user and week.
/// </summary>
public record GetGroceryListQuery(string UserId, DateOnly WeekStart) : IQuery<GroceryList>;

/// <summary>
/// Handles retrieving a grocery list from MongoDB.
/// Returns NotFound if no list has been generated for the specified week.
/// </summary>
public class GetGroceryListQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetGroceryListQuery, GroceryList>
{
	public async Task<Result<GroceryList>> HandleAsync(
		GetGroceryListQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var weekStartStr = GroceryListHelpers.NormalizeToMonday(query.WeekStart).ToString("yyyy-MM-dd");
			var entity = await db.GroceryLists
				.FirstOrDefaultAsync(g => g.UserId == query.UserId && g.WeekStart == weekStartStr, cancellationToken);

			if (entity is null)
			{
				return Result<GroceryList>.Failure(
					new Error(ErrorCodes.NotFound, "No grocery list found for the specified week."));
			}

			return Result<GroceryList>.Success(
				GroceryListHelpers.MapToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<GroceryList>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve grocery list.", ex));
		}
	}
}
