using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Queries;

namespace MealPlanner.Api.Tests.Features.MealPlans.Mappers;

public class MealPlanQueryMappersTests
{
	[Theory]
	[InlineData(2026, 2, 23, 2026, 2, 23)]
	[InlineData(2026, 2, 24, 2026, 2, 23)]
	[InlineData(2026, 3, 1, 2026, 2, 23)]
	public void NormalizeToMonday_NormalizesExpectedDate(int y, int m, int d, int ey, int em, int ed)
	{
		var normalized = GetMealPlanQueryHandler.NormalizeToMonday(new DateOnly(y, m, d));
		Assert.Equal(new DateOnly(ey, em, ed), normalized);
	}

	[Fact]
	public void MapToDomain_MapsNestedSlotsAndOptionRecipeId()
	{
		var doc = new MealPlanDocument
		{
			Id = "mp1",
			UserId = "u1",
			WeekStart = "2026-02-23",
			Days =
			[
				new DayPlanDocument
				{
					Day = "Monday",
					Slots = new Dictionary<string, List<MealSlotItemDocument>>
					{
						["Breakfast"] =
						[
							new MealSlotItemDocument { RecipeId = "r1", Name = "Oats", Servings = 3 },
							new MealSlotItemDocument { RecipeId = null, Name = "Coffee" }
						],
						["Lunch"] = [],
						["Supper"] = [],
						["Snacks"] = []
					}
				}
			],
			CreatedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc)
		};

		var domain = GetMealPlanQueryHandler.MapToDomain(doc);

		Assert.Equal("mp1", domain.Id);
		Assert.Equal(new DateOnly(2026, 2, 23), domain.WeekStart);
		Assert.Equal(DayOfWeek.Monday, domain.Days[0].Day);
		Assert.True(domain.Days[0].Slots[MealCategory.Breakfast][0].RecipeId.HasValue);
		Assert.Equal(3, domain.Days[0].Slots[MealCategory.Breakfast][0].Servings);
		Assert.False(domain.Days[0].Slots[MealCategory.Breakfast][1].RecipeId.HasValue);
		Assert.Equal(1, domain.Days[0].Slots[MealCategory.Breakfast][1].Servings);
	}
}
