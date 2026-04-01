using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Commands;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Tests.Features.MealPlans.Commands;

public class UpdateDaySlotTests
{
	private static MealPlanEntity ExistingPlan() => new()
	{
		Id = Guid.NewGuid(),
		UserId = "u1",
		WeekStart = "2026-02-23",
		Days =
		[
			new DayPlanData
			{
				Day = "Monday",
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

	[Fact]
	public async Task HandleAsync_UpdatesSlot_WhenPlanAndDayExist()
	{
		var context = TestDbContextFactory.CreateContext(seed: db => db.MealPlans.Add(ExistingPlan()));

		var handler = new UpdateDaySlotCommandHandler(context);
		var result = await handler.HandleAsync(
			new UpdateDaySlotCommand(
				"u1",
				new DateOnly(2026, 2, 23),
				DayOfWeek.Monday,
				MealCategory.Breakfast,
				[new MealSlotItem(Option<string>.Some("r1"), "Oats", 4)]),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Single(result.Value.Days[0].Slots[MealCategory.Breakfast]);
		Assert.Equal("r1", result.Value.Days[0].Slots[MealCategory.Breakfast][0].RecipeId.GetValueOrNull());
		Assert.Equal(4, result.Value.Days[0].Slots[MealCategory.Breakfast][0].Servings);
	}

	[Fact]
	public async Task HandleAsync_PersistsUpdatedSlot_WhenReloadedFromDatabase()
	{
		const string databaseName = "update-day-slot-persists-updates";
		TestDbContextFactory.CreateContext(seed: db => db.MealPlans.Add(ExistingPlan()), databaseName: databaseName).Dispose();

		using (var updateContext = TestDbContextFactory.CreateContext(databaseName: databaseName))
		{
			var handler = new UpdateDaySlotCommandHandler(updateContext);
			var result = await handler.HandleAsync(
				new UpdateDaySlotCommand(
					"u1",
					new DateOnly(2026, 2, 23),
					DayOfWeek.Monday,
					MealCategory.Breakfast,
					[new MealSlotItem(Option<string>.Some("recipe-123"), "Avocado Toast", 2)]),
				TestContext.Current.CancellationToken);

			Assert.True(result.IsSuccess);
		}

		using var reloadContext = TestDbContextFactory.CreateContext(databaseName: databaseName);
		var reloadedPlan = await reloadContext.MealPlans.FirstAsync(
			p => p.UserId == "u1" && p.WeekStart == "2026-02-23",
			TestContext.Current.CancellationToken);

		var monday = reloadedPlan.Days.First(d => d.Day == "Monday");
		Assert.Contains("Breakfast", monday.Slots.Keys);
		var items = monday.Slots["Breakfast"];
		Assert.Single(items);
		Assert.Equal("recipe-123", items[0].RecipeId);
		Assert.Equal("Avocado Toast", items[0].Name);
		Assert.Equal(2, items[0].Servings);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenDayMissing()
	{
		var plan = ExistingPlan();
		plan.Days = [];
		var context = TestDbContextFactory.CreateContext(seed: db => db.MealPlans.Add(plan));

		var handler = new UpdateDaySlotCommandHandler(context);
		var result = await handler.HandleAsync(
			new UpdateDaySlotCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, []),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenAnyItemServingsIsLessThanOne()
	{
		var handler = new UpdateDaySlotCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new UpdateDaySlotCommand(
				"u1",
				new DateOnly(2026, 2, 23),
				DayOfWeek.Monday,
				MealCategory.Breakfast,
				[new MealSlotItem(Option<string>.Some("r1"), "Oats", 0)]),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new UpdateDaySlotCommandHandler(context);
		var result = await handler.HandleAsync(
			new UpdateDaySlotCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, []),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
