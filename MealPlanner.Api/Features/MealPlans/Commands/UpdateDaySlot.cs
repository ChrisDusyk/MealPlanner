using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Queries;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.MealPlans.Commands;

/// <summary>
/// Command to set the items for a specific day and meal category within a weekly plan.
/// </summary>
public record UpdateDaySlotCommand(
	string UserId,
	DateOnly WeekStart,
	DayOfWeek Day,
	MealCategory Category,
	List<MealSlotItem> Items
) : ICommand<MealPlan>;

/// <summary>
/// Handles replacing all items in a single day+category slot.
/// </summary>
public class UpdateDaySlotCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<UpdateDaySlotCommand, MealPlan>
{
	public async Task<Result<MealPlan>> HandleAsync(
		UpdateDaySlotCommand command,
		CancellationToken cancellationToken = default)
	{
		if (command.Items.Any(item => item.Servings < 1))
			return Result<MealPlan>.Failure(
				new Error(ErrorCodes.ValidationFailed, "All meal slot item servings must be at least 1."));

		try
		{
			var weekStart = GetMealPlanQueryHandler.NormalizeToMonday(command.WeekStart);
			var weekStartStr = weekStart.ToString("yyyy-MM-dd");

			// Ensure the plan exists (auto-create via GetMealPlan query logic)
			var entity = await db.MealPlans
				.FirstOrDefaultAsync(p => p.UserId == command.UserId && p.WeekStart == weekStartStr, cancellationToken);

			if (entity is null)
			{
				// Auto-create by piggybacking on the query handler
				var queryHandler = new GetMealPlanQueryHandler(db);
				var createResult = await queryHandler.HandleAsync(
					new GetMealPlanQuery(command.UserId, weekStart), cancellationToken);

				if (!createResult.IsSuccess)
					return createResult;

				entity = await db.MealPlans
					.FirstOrDefaultAsync(p => p.UserId == command.UserId && p.WeekStart == weekStartStr, cancellationToken);
			}

			if (entity is null)
				return Result<MealPlan>.Failure(
					new Error(ErrorCodes.DatabaseError, "Failed to locate or create meal plan."));

			// Find the target day and update the category slot
			var dayStr = command.Day.ToString();
			var categoryStr = command.Category.ToString();
			var dayPlan = entity.Days.FirstOrDefault(d => d.Day == dayStr);

			if (dayPlan is null)
				return Result<MealPlan>.Failure(
					new Error(ErrorCodes.ValidationFailed, $"Day '{dayStr}' not found in plan."));

			dayPlan.Slots[categoryStr] = command.Items.Select(item => new MealSlotItemData
			{
				RecipeId = item.RecipeId.GetValueOrNull(),
				Name = item.Name,
				Servings = item.Servings
			}).ToList();
			db.Entry(entity).Property(x => x.Days).IsModified = true;

			entity.UpdatedAt = DateTime.UtcNow;

			await db.SaveChangesAsync(cancellationToken);

			return Result<MealPlan>.Success(GetMealPlanQueryHandler.MapToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<MealPlan>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to update meal slot.", ex));
		}
	}
}
