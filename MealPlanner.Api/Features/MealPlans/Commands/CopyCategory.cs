using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Queries;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

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
public class CopyCategoryCommandHandler(MealPlannerDbContext db)
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

			var entity = await db.MealPlans
				.FirstOrDefaultAsync(p => p.UserId == command.UserId && p.WeekStart == weekStartStr, cancellationToken);

			if (entity is null)
				return Result<MealPlan>.Failure(
					new Error(ErrorCodes.NotFound, "Meal plan not found for the specified week."));

			var sourceDayStr = command.SourceDay.ToString();
			var categoryStr = command.Category.ToString();

			var sourceDay = entity.Days.FirstOrDefault(d => d.Day == sourceDayStr);
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
				var targetDay = entity.Days.FirstOrDefault(d => d.Day == targetDayStr);

				if (targetDay is null) continue;

				targetDay.Slots[categoryStr] = sourceItems
					.Select(item => new MealSlotItemData
					{
						RecipeId = item.RecipeId,
						Name = item.Name,
						Servings = item.Servings
					})
					.ToList();
			}

			entity.UpdatedAt = DateTime.UtcNow;

			await db.SaveChangesAsync(cancellationToken);

			return Result<MealPlan>.Success(GetMealPlanQueryHandler.MapToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<MealPlan>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to copy meal category.", ex));
		}
	}
}
