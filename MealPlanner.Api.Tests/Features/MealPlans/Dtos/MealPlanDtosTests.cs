using MealPlanner.Api.Features.MealPlans.Dtos;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.MealPlans.Dtos;

public class MealPlanDtosTests
{
	[Fact]
	public void MealSlotItemDto_ToDomain_And_FromDomain_Work()
	{
		var dto = new MealSlotItemDto("r1", "Oats", 3);
		var domain = dto.ToDomain();
		var roundTrip = MealSlotItemDto.FromDomain(domain);

		Assert.True(domain.RecipeId.HasValue);
		Assert.Equal("r1", domain.RecipeId.Value);
		Assert.Equal("Oats", roundTrip.Name);
		Assert.Equal("r1", roundTrip.RecipeId);
		Assert.Equal(3, domain.Servings);
		Assert.Equal(3, roundTrip.Servings);
	}

	[Fact]
	public void MealSlotItemDto_DefaultsServingsToOne()
	{
		var dto = new MealSlotItemDto("r1", "Oats");
		var domain = dto.ToDomain();

		Assert.Equal(1, domain.Servings);
	}

	[Fact]
	public void DayPlanDto_FromDomain_MapsEnumKeysToStrings()
	{
		var day = new DayPlan(
			DayOfWeek.Monday,
			new Dictionary<MealCategory, List<MealSlotItem>>
			{
				[MealCategory.Breakfast] = [new MealSlotItem(Option<string>.None(), "Coffee")],
				[MealCategory.Lunch] = [],
				[MealCategory.Supper] = [],
				[MealCategory.Snacks] = []
			});

		var dto = DayPlanDto.FromDomain(day);

		Assert.Equal("Monday", dto.Day);
		Assert.True(dto.Slots.ContainsKey("Breakfast"));
		Assert.Single(dto.Slots["Breakfast"]);
	}

	[Fact]
	public void MealPlanResponse_FromDomain_MapsWeekFormat()
	{
		var plan = new MealPlan(
			"mp1",
			"u1",
			new DateOnly(2026, 2, 23),
			[new DayPlan(DayOfWeek.Monday, new Dictionary<MealCategory, List<MealSlotItem>> { [MealCategory.Breakfast] = [], [MealCategory.Lunch] = [], [MealCategory.Supper] = [], [MealCategory.Snacks] = [] })],
			new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc));

		var response = MealPlanResponse.FromDomain(plan);

		Assert.Equal("mp1", response.Id);
		Assert.Equal("2026-02-23", response.WeekStart);
		Assert.Single(response.Days);
	}
}
