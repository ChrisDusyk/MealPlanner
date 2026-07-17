using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.MealPlans.Queries;

/// <summary>
/// Query to retrieve a family's meal plan for a given week. Auto-creates an
/// empty plan if none exists.
/// </summary>
public record GetMealPlanQuery(Guid FamilyGroupId, DateOnly WeekStart) : IQuery<MealPlan>;

/// <summary>
/// Handles retrieving (or auto-creating) a weekly meal plan.
/// </summary>
public class GetMealPlanQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetMealPlanQuery, MealPlan>
{
	private static readonly MealCategory[] AllCategories =
		[MealCategory.Breakfast, MealCategory.Lunch, MealCategory.Supper, MealCategory.Snacks];

	private static readonly DayOfWeek[] WeekDays =
		[DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
		 DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

	public async Task<Result<MealPlan>> HandleAsync(
		GetMealPlanQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var weekStart = NormalizeToMonday(query.WeekStart);
			var weekStartStr = weekStart.ToString("yyyy-MM-dd");

			var entity = await db.MealPlans
				.FirstOrDefaultAsync(
					p => p.FamilyGroupId == query.FamilyGroupId && p.WeekStart == weekStartStr,
					cancellationToken);

			if (entity is not null)
				return Result<MealPlan>.Success(MapToDomain(entity));

			// Auto-create empty plan for this week
			var now = DateTime.UtcNow;
			entity = new MealPlanEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = query.FamilyGroupId,
				WeekStart = weekStartStr,
				Days = WeekDays.Select(day => new DayPlanData
				{
					Day = day.ToString(),
					Slots = AllCategories.ToDictionary(
						c => c.ToString(),
						_ => new List<MealSlotItemData>()
					)
				}).ToList(),
				CreatedAt = now,
				UpdatedAt = now
			};

			db.MealPlans.Add(entity);
			await db.SaveChangesAsync(cancellationToken);
			return Result<MealPlan>.Success(MapToDomain(entity));
		}
		catch (Exception ex)
		{
			return Result<MealPlan>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve meal plan.", ex));
		}
	}

	/// <summary>
	/// Normalizes any date to the Monday of the week it falls in.
	/// </summary>
	internal static DateOnly NormalizeToMonday(DateOnly date)
	{
		var dayOfWeek = date.DayOfWeek;
		var offset = dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;
		return date.AddDays(-offset);
	}

	internal static MealPlan MapToDomain(MealPlanEntity entity) =>
		new(
			Id: entity.Id.ToString(),
			FamilyGroupId: entity.FamilyGroupId.ToString(),
			WeekStart: DateOnly.ParseExact(entity.WeekStart, "yyyy-MM-dd"),
			Days: entity.Days.Select(d => new DayPlan(
				Day: Enum.Parse<DayOfWeek>(d.Day, ignoreCase: true),
				Slots: d.Slots.ToDictionary(
					kvp => Enum.Parse<MealCategory>(kvp.Key, ignoreCase: true),
					kvp => kvp.Value.Select(item => new MealSlotItem(
						RecipeId: Option<string>.From(item.RecipeId),
						Name: item.Name,
						Servings: item.Servings
					)).ToList()
				)
			)).ToList(),
			CreatedAt: entity.CreatedAt,
			UpdatedAt: entity.UpdatedAt
		);
}
