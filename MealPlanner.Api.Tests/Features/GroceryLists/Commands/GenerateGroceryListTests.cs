using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Commands;

public class GenerateGroceryListTests
{
	private static MealPlanEntity CreateMealPlan(string userId = "u1", string weekStart = "2026-02-23") =>
		new()
		{
			Id = Guid.NewGuid(),
			UserId = userId,
			WeekStart = weekStart,
			Days =
			[
				new DayPlanData
				{
					Day = "Monday",
					Slots = new Dictionary<string, List<MealSlotItemData>>
					{
						["Supper"] =
						[
							new MealSlotItemData { RecipeId = Guid.Parse("11111111-1111-1111-1111-111111111111").ToString(), Name = "Pasta", Servings = 1 },
							new MealSlotItemData { Name = "Bananas" },
							new MealSlotItemData { Name = "bananas" }
						]
					}
				}
			],
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

	private static RecipeEntity CreateRecipe() =>
		new()
		{
			Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
			UserId = "u1",
			Name = "Pasta",
			Description = string.Empty,
			Servings = 1,
			Ingredients =
			[
				new IngredientData { Name = "Tomato", Quantity = 2, Unit = "pcs" },
				new IngredientData { Name = "Tomato", Quantity = 1, Unit = "pcs" }
			],
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenMealPlanMissing()
	{
		var handler = new GenerateGroceryListCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(
			new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_InsertsNewList_WhenNoExistingList()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.MealPlans.Add(CreateMealPlan());
			db.Recipes.Add(CreateRecipe());
		});

		var handler = new GenerateGroceryListCommandHandler(context);
		var result = await handler.HandleAsync(
			new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(context.GroceryLists);
		Assert.Contains(context.GroceryLists.Single().Items, i => i.Name == "Tomato" && i.Quantity == 3 && i.Unit == "pcs");
		Assert.Equal(1, context.GroceryLists.Single().Items.Count(i => i.Name.Equals("Bananas", StringComparison.OrdinalIgnoreCase)));
	}

	[Fact]
	public async Task HandleAsync_ReplacesExistingList_WhenPresent()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.MealPlans.Add(CreateMealPlan());
			db.Recipes.Add(CreateRecipe());
			db.GroceryLists.Add(new GroceryListEntity
			{
				Id = Guid.NewGuid(),
				UserId = "u1",
				WeekStart = "2026-02-23",
				Items = [],
				PantryStapleItems = [],
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow.AddDays(-1)
			});
		});

		var handler = new GenerateGroceryListCommandHandler(context);
		var result = await handler.HandleAsync(
			new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(context.GroceryLists);
		Assert.NotEmpty(context.GroceryLists.Single().Items);
	}

	[Fact]
	public async Task HandleAsync_AutoSharesFromFriendPreferences_WhenEnabledAndFriendshipActive()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.MealPlans.Add(CreateMealPlan());
			db.Recipes.Add(CreateRecipe());
			db.FriendAutoSharePreferences.Add(new FriendAutoSharePreferenceEntity
			{
				Id = Guid.NewGuid(),
				UserId = "u1",
				FriendUserId = "u2",
				AutoShareMealPlans = false,
				AutoShareGroceryLists = true,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
			db.Friendships.Add(new FriendshipEntity
			{
				Id = Guid.NewGuid(),
				UserAId = "u1",
				UserBId = "u2",
				CreatedAt = DateTime.UtcNow
			});
		});

		var handler = new GenerateGroceryListCommandHandler(context);
		var result = await handler.HandleAsync(
			new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Single(context.GroceryListShares.Where(s => s.OwnerUserId == "u1" && s.SharedWithUserId == "u2"));
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new GenerateGroceryListCommandHandler(context);
		var result = await handler.HandleAsync(
			new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
