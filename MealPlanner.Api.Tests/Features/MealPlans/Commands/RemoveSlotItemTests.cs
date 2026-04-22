using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Commands;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Tests.Features.MealPlans.Commands;

public class RemoveSlotItemTests
{
	private static MealPlanEntity BasePlan() => new()
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
					["Breakfast"] = [new MealSlotItemData { RecipeId = "r1", Name = "Oats", Servings = 1 }],
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
	public async Task HandleAsync_ReturnsNotFound_WhenPlanMissing()
	{
		var handler = new RemoveSlotItemCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new RemoveSlotItemCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, 0),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenIndexOutOfRange()
	{
		var context = TestDbContextFactory.CreateContext(seed: db => db.MealPlans.Add(BasePlan()));

		var handler = new RemoveSlotItemCommandHandler(context);
		var result = await handler.HandleAsync(
			new RemoveSlotItemCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, 5),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_RemovesItem_WhenIndexValid()
	{
		var context = TestDbContextFactory.CreateContext(seed: db => db.MealPlans.Add(BasePlan()));

		var handler = new RemoveSlotItemCommandHandler(context);
		var result = await handler.HandleAsync(
			new RemoveSlotItemCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, 0),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Empty(result.Value.Days[0].Slots[MealCategory.Breakfast]);
	}

	[Fact]
	public async Task HandleAsync_PersistsRemovedItem_WhenReloadedFromDatabase()
	{
		const string databaseName = "remove-slot-item-persists-updates";
		TestDbContextFactory.CreateContext(seed: db => db.MealPlans.Add(BasePlan()), databaseName: databaseName).Dispose();

		using (var removeContext = TestDbContextFactory.CreateContext(databaseName: databaseName))
		{
			var handler = new RemoveSlotItemCommandHandler(removeContext);
			var result = await handler.HandleAsync(
				new RemoveSlotItemCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, 0),
				TestContext.Current.CancellationToken);

			Assert.True(result.IsSuccess);
		}

		using var reloadContext = TestDbContextFactory.CreateContext(databaseName: databaseName);
		var reloadedPlan = await reloadContext.MealPlans.FirstAsync(
			p => p.UserId == "u1" && p.WeekStart == "2026-02-23",
			TestContext.Current.CancellationToken);

		var monday = reloadedPlan.Days.First(d => d.Day == "Monday");
		Assert.Contains("Breakfast", monday.Slots.Keys);
		Assert.Empty(monday.Slots["Breakfast"]);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new RemoveSlotItemCommandHandler(context);
		var result = await handler.HandleAsync(
			new RemoveSlotItemCommand("u1", new DateOnly(2026, 2, 23), DayOfWeek.Monday, MealCategory.Breakfast, 0),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
