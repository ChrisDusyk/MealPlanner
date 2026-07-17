using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Commands;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.MealPlans.Commands;

public class CopyCategoryTests
{
	private static MealPlanEntity PlanWithSourceItems() => new()
	{
		Id = Guid.NewGuid(),
		FamilyGroupId = TestIds.Family("u1"),
		WeekStart = "2026-02-23",
		Days =
		[
			new DayPlanData
			{
				Day = "Monday",
				Slots = new Dictionary<string, List<MealSlotItemData>>
				{
					["Breakfast"] = [new MealSlotItemData { RecipeId = "r1", Name = "Oats", Servings = 2 }],
					["Lunch"] = [],
					["Supper"] = [],
					["Snacks"] = []
				}
			},
			new DayPlanData
			{
				Day = "Tuesday",
				Slots = new Dictionary<string, List<MealSlotItemData>>
				{
					["Breakfast"] = [],
					["Lunch"] = [],
					["Supper"] = [],
					["Snacks"] = []
				}
			}
		],
		CreatedAt = DateTime.UtcNow,
		UpdatedAt = DateTime.UtcNow
	};

	private static MealPlanEntity PlanWithLowerCaseSlotKeys() => new()
	{
		Id = Guid.NewGuid(),
		FamilyGroupId = TestIds.Family("u1"),
		WeekStart = "2026-02-23",
		Days =
		[
			new DayPlanData
			{
				Day = "Wednesday",
				Slots = new Dictionary<string, List<MealSlotItemData>>
				{
					["breakfast"] = [],
					["lunch"] = [new MealSlotItemData { RecipeId = "r2", Name = "Soup", Servings = 1 }],
					["supper"] = [],
					["snacks"] = []
				}
			},
			new DayPlanData
			{
				Day = "Thursday",
				Slots = new Dictionary<string, List<MealSlotItemData>>
				{
					["breakfast"] = [],
					["lunch"] = [],
					["supper"] = [],
					["snacks"] = []
				}
			}
		],
		CreatedAt = DateTime.UtcNow,
		UpdatedAt = DateTime.UtcNow
	};

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenNoTargets()
	{
		var handler = new CopyCategoryCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new CopyCategoryCommand(TestIds.Family("u1"), new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, []),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenPlanMissing()
	{
		var handler = new CopyCategoryCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new CopyCategoryCommand(TestIds.Family("u1"), new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, [DayOfWeek.Tuesday]),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_CopiesSourceCategoryToTargets()
	{
		var context = TestDbContextFactory.CreateContext(seed: db => db.MealPlans.Add(PlanWithSourceItems()));

		var handler = new CopyCategoryCommandHandler(context);
		var result = await handler.HandleAsync(
			new CopyCategoryCommand(TestIds.Family("u1"), new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, [DayOfWeek.Tuesday]),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		var copiedItems = result.Value.Days.First(d => d.Day == DayOfWeek.Tuesday).Slots[MealCategory.Breakfast];
		Assert.Single(copiedItems);
		Assert.Equal(2, copiedItems[0].Servings);
	}

	[Fact]
	public async Task HandleAsync_CopiesSourceCategory_WhenStoredSlotKeysUseDifferentCasing()
	{
		var context = TestDbContextFactory.CreateContext(seed: db => db.MealPlans.Add(PlanWithLowerCaseSlotKeys()));

		var handler = new CopyCategoryCommandHandler(context);
		var result = await handler.HandleAsync(
			new CopyCategoryCommand(TestIds.Family("u1"), new DateOnly(2026, 2, 23), DayOfWeek.Wednesday, MealCategory.Lunch, [DayOfWeek.Thursday]),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		var copiedItems = result.Value.Days.First(d => d.Day == DayOfWeek.Thursday).Slots[MealCategory.Lunch];
		Assert.Single(copiedItems);
		Assert.Equal("Soup", copiedItems[0].Name);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new CopyCategoryCommandHandler(context);
		var result = await handler.HandleAsync(
			new CopyCategoryCommand(TestIds.Family("u1"), new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, [DayOfWeek.Tuesday]),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
