using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.MealPlans.Queries;

/// <summary>
/// Query to retrieve a meal plan for a given week. Auto-creates an empty plan if none exists.
/// </summary>
public record GetMealPlanQuery(string UserId, DateOnly WeekStart) : IQuery<MealPlan>;

/// <summary>
/// Handles retrieving (or auto-creating) a weekly meal plan from MongoDB.
/// </summary>
public class GetMealPlanQueryHandler(IMongoClient mongoClient)
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

			var collection = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<MealPlanDocument>("mealplans");

			var document = await collection
				.Find(p => p.UserId == query.UserId && p.WeekStart == weekStartStr)
				.FirstOrDefaultAsync(cancellationToken);

			if (document is not null)
				return Result<MealPlan>.Success(MapToDomain(document));

			// Auto-create empty plan for this week
			var now = DateTime.UtcNow;
			document = new MealPlanDocument
			{
				UserId = query.UserId,
				WeekStart = weekStartStr,
				Days = WeekDays.Select(day => new DayPlanDocument
				{
					Day = day.ToString(),
					Slots = AllCategories.ToDictionary(
						c => c.ToString(),
						_ => new List<MealSlotItemDocument>()
					)
				}).ToList(),
				CreatedAt = now,
				UpdatedAt = now
			};

			await collection.InsertOneAsync(document, cancellationToken: cancellationToken);
			return Result<MealPlan>.Success(MapToDomain(document));
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

	internal static MealPlan MapToDomain(MealPlanDocument doc) =>
		new(
			Id: doc.Id!,
			UserId: doc.UserId,
			WeekStart: DateOnly.ParseExact(doc.WeekStart, "yyyy-MM-dd"),
			Days: doc.Days.Select(d => new DayPlan(
				Day: Enum.Parse<DayOfWeek>(d.Day),
				Slots: d.Slots.ToDictionary(
					kvp => Enum.Parse<MealCategory>(kvp.Key),
					kvp => kvp.Value.Select(item => new MealSlotItem(
						RecipeId: Option<string>.From(item.RecipeId),
						Name: item.Name
					)).ToList()
				)
			)).ToList(),
			CreatedAt: doc.CreatedAt,
			UpdatedAt: doc.UpdatedAt
		);
}
