using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.GroceryLists.Queries;

/// <summary>
/// Query to retrieve an existing grocery list for a given user and week.
/// </summary>
public record GetGroceryListQuery(string UserId, DateOnly WeekStart) : IQuery<GroceryList>;

/// <summary>
/// Handles retrieving a grocery list from MongoDB.
/// Returns NotFound if no list has been generated for the specified week.
/// </summary>
public class GetGroceryListQueryHandler(IMongoClient mongoClient)
	: IQueryHandler<GetGroceryListQuery, GroceryList>
{
	public async Task<Result<GroceryList>> HandleAsync(
		GetGroceryListQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var weekStartStr = query.WeekStart.ToString("yyyy-MM-dd");
			var collection = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<GroceryListDocument>("grocerylists");

			var document = await collection
				.Find(g => g.UserId == query.UserId && g.WeekStart == weekStartStr)
				.FirstOrDefaultAsync(cancellationToken);

			if (document is null)
			{
				return Result<GroceryList>.Failure(
					new Error(ErrorCodes.NotFound, "No grocery list found for the specified week."));
			}

			return Result<GroceryList>.Success(
				GenerateGroceryListCommandHandler.MapToDomain(document));
		}
		catch (Exception ex)
		{
			return Result<GroceryList>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve grocery list.", ex));
		}
	}
}
