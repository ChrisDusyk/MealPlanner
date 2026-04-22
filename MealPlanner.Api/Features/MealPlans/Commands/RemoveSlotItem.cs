using MealPlanner.Api.Data;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Queries;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.MealPlans.Commands;

/// <summary>
/// Command to remove a single item from a day+category slot by its index.
/// </summary>
public record RemoveSlotItemCommand(
	string UserId,
	DateOnly WeekStart,
	DayOfWeek Day,
	MealCategory Category,
	int ItemIndex
) : ICommand<MealPlan>;

/// <summary>
/// Handles removing one item from a meal slot by index position.
/// </summary>
public class RemoveSlotItemCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<RemoveSlotItemCommand, MealPlan>
{
	public async Task<Result<MealPlan>> HandleAsync(
		RemoveSlotItemCommand command,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var weekStart = GetMealPlanQueryHandler.NormalizeToMonday(command.WeekStart);
			var weekStartStr = weekStart.ToString("yyyy-MM-dd");

			var entity = await db.MealPlans
				.FirstOrDefaultAsync(p => p.UserId == command.UserId && p.WeekStart == weekStartStr, cancellationToken);

			if (entity is null)
				return Result<MealPlan>.Failure(
					new Error(ErrorCodes.NotFound, "Meal plan not found for the specified week."));

			var dayStr = command.Day.ToString();
			var categoryStr = command.Category.ToString();

			var dayPlan = entity.Days.FirstOrDefault(d => d.Day == dayStr);
			if (dayPlan is null)
				return Result<MealPlan>.Failure(
					new Error(ErrorCodes.ValidationFailed, $"Day '{dayStr}' not found in plan."));

			if (!dayPlan.Slots.TryGetValue(categoryStr, out var items))
				return Result<MealPlan>.Failure(
					new Error(ErrorCodes.ValidationFailed,
						$"Category '{categoryStr}' not found for {dayStr}."));

			if (command.ItemIndex < 0 || command.ItemIndex >= items.Count)
				return Result<MealPlan>.Failure(
					new Error(ErrorCodes.ValidationFailed,
						$"Item index {command.ItemIndex} is out of range."));

			items.RemoveAt(command.ItemIndex);
			db.Entry(entity).Property(x => x.Days).IsModified = true;
			entity.UpdatedAt = DateTime.UtcNow;

			await db.SaveChangesAsync(cancellationToken);

			return Result<MealPlan>.Success(GetMealPlanQueryHandler.MapToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<MealPlan>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to remove slot item.", ex));
		}
	}
}
