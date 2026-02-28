using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Queries;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.MealPlans.Commands;

/// <summary>
/// Command to copy all items from one day+category to the same category on multiple target days.
/// </summary>
public record CopyCategoryCommand(
	string UserId,
	DateOnly WeekStart,
	DayOfWeek SourceDay,
	MealCategory Category,
	List<DayOfWeek> TargetDays
) : ICommand<MealPlan>;

/// <summary>
/// Handles cloning a meal category's items from a source day into multiple target days.
/// </summary>
public class CopyCategoryCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<CopyCategoryCommand, MealPlan>
{
	public async Task<Result<MealPlan>> HandleAsync(
		CopyCategoryCommand command,
		CancellationToken cancellationToken = default)
	{
		if (command.TargetDays.Count == 0)
			return Result<MealPlan>.Failure(
				new Error(ErrorCodes.ValidationFailed, "At least one target day is required."));

		try
		{
			var weekStart = GetMealPlanQueryHandler.NormalizeToMonday(command.WeekStart);
			var weekStartStr = weekStart.ToString("yyyy-MM-dd");

			var collection = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<MealPlanDocument>("mealplans");

			var document = await collection
				.Find(p => p.UserId == command.UserId && p.WeekStart == weekStartStr)
				.FirstOrDefaultAsync(cancellationToken);

			if (document is null)
				return Result<MealPlan>.Failure(
					new Error(ErrorCodes.NotFound, "Meal plan not found for the specified week."));

			var sourceDayStr = command.SourceDay.ToString();
			var categoryStr = command.Category.ToString();

			var sourceDay = document.Days.FirstOrDefault(d => d.Day == sourceDayStr);
			if (sourceDay is null)
				return Result<MealPlan>.Failure(
					new Error(ErrorCodes.ValidationFailed, $"Source day '{sourceDayStr}' not found."));

			if (!sourceDay.Slots.TryGetValue(categoryStr, out var sourceItems) || sourceItems.Count == 0)
				return Result<MealPlan>.Failure(
					new Error(ErrorCodes.ValidationFailed,
						$"No items in {categoryStr} on {sourceDayStr} to copy."));

			// Clone source items to each target day
			foreach (var targetDayOfWeek in command.TargetDays)
			{
				var targetDayStr = targetDayOfWeek.ToString();
				var targetDay = document.Days.FirstOrDefault(d => d.Day == targetDayStr);

				if (targetDay is null) continue;

				targetDay.Slots[categoryStr] = sourceItems
					.Select(item => new MealSlotItemDocument
					{
						RecipeId = item.RecipeId,
						Name = item.Name,
						Servings = item.Servings
					})
					.ToList();
			}

			document.UpdatedAt = DateTime.UtcNow;

			await collection.ReplaceOneAsync(
				p => p.Id == document.Id,
				document,
				cancellationToken: cancellationToken);

			return Result<MealPlan>.Success(GetMealPlanQueryHandler.MapToDomain(document));
		}
		catch (Exception ex)
		{
			return Result<MealPlan>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to copy meal category.", ex));
		}
	}
}
