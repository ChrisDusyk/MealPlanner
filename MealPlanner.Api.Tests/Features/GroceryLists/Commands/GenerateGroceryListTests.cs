using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Features.MealPlans.Commands;
using MealPlanner.Api.Features.MealPlans.Models;
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

	private static MealPlanEntity CreateEmptyMealPlanForFlow(string userId = "u1", string weekStart = "2026-02-23") =>
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
						["Breakfast"] = [],
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
				},
				new DayPlanData
				{
					Day = "Wednesday",
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

	private static MealPlanEntity CreateMealPlanWithRepeatedRecipeServings(string userId = "u1", string weekStart = "2026-02-23") =>
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
							new MealSlotItemData { RecipeId = Guid.Parse("11111111-1111-1111-1111-111111111111").ToString(), Name = "Pasta", Servings = 2 }
						]
					}
				},
				new DayPlanData
				{
					Day = "Tuesday",
					Slots = new Dictionary<string, List<MealSlotItemData>>
					{
						["Supper"] =
						[
							new MealSlotItemData { RecipeId = Guid.Parse("11111111-1111-1111-1111-111111111111").ToString(), Name = "Pasta", Servings = 2 }
						]
					}
				},
				new DayPlanData
				{
					Day = "Wednesday",
					Slots = new Dictionary<string, List<MealSlotItemData>>
					{
						["Supper"] =
						[
							new MealSlotItemData { RecipeId = Guid.Parse("11111111-1111-1111-1111-111111111111").ToString(), Name = "Pasta", Servings = 2 }
						]
					}
				}
			],
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

	private static RecipeEntity CreateSixServingRecipe() =>
		new()
		{
			Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
			UserId = "u1",
			Name = "Pasta",
			Description = string.Empty,
			Servings = 6,
			Ingredients =
			[
				new IngredientData { Name = "Tomato", Quantity = 3, Unit = "pcs" },
				new IngredientData { Name = "Garlic", Quantity = 6, Unit = "cloves" }
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
	public async Task HandleAsync_ScalesIngredientQuantities_BySlotServingsAcrossMultipleDays()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.MealPlans.Add(CreateMealPlanWithRepeatedRecipeServings());
			db.Recipes.Add(CreateSixServingRecipe());
		});

		var handler = new GenerateGroceryListCommandHandler(context);
		var result = await handler.HandleAsync(
			new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		var items = context.GroceryLists.Single().Items;
		Assert.Contains(items, i =>
			i.Name == "Tomato"
			&& i.Unit == "pcs"
			&& decimal.Abs(i.Quantity - 3m) < 0.000001m);
		Assert.Contains(items, i =>
			i.Name == "Garlic"
			&& i.Unit == "cloves"
			&& decimal.Abs(i.Quantity - 6m) < 0.000001m);
	}

	[Fact]
	public async Task Flow_SharedPlanReadWrite_UpdateCopyGenerate_PersistsServingsScalingForOwner()
	{
		const string databaseName = "grocery-flow-shared-slot-copy-generate";
		var week = new DateOnly(2026, 2, 23);
		const string ownerUserId = "owner-1";
		const string collaboratorUserId = "collab-1";

		TestDbContextFactory.CreateContext(seed: db =>
		{
			db.MealPlans.Add(CreateEmptyMealPlanForFlow(userId: ownerUserId));
			db.Recipes.Add(new RecipeEntity
			{
				Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
				UserId = ownerUserId,
				Name = "Pasta",
				Description = string.Empty,
				Servings = 6,
				Ingredients =
				[
					new IngredientData { Name = "Tomato", Quantity = 3, Unit = "pcs" },
					new IngredientData { Name = "Garlic", Quantity = 6, Unit = "cloves" }
				],
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
			db.MealPlanShares.Add(new MealPlanShareEntity
			{
				Id = Guid.NewGuid(),
				OwnerUserId = ownerUserId,
				SharedWithUserId = collaboratorUserId,
				WeekStart = "2026-02-23",
				Permission = nameof(SharePermission.ReadWrite),
				DismissedByRecipient = false,
				SharedAt = DateTime.UtcNow
			});
		}, databaseName: databaseName).Dispose();

		using (var accessCheckContext = TestDbContextFactory.CreateContext(databaseName: databaseName))
		{
			var hasReadWriteShare = accessCheckContext.MealPlanShares.Any(
				s => s.OwnerUserId == ownerUserId
				     && s.SharedWithUserId == collaboratorUserId
				     && s.WeekStart == "2026-02-23"
				     && s.Permission == nameof(SharePermission.ReadWrite)
				     && !s.DismissedByRecipient);
			Assert.True(hasReadWriteShare);
		}

		using (var updateContext = TestDbContextFactory.CreateContext(databaseName: databaseName))
		{
			var updateHandler = new UpdateDaySlotCommandHandler(updateContext);
			var updateResult = await updateHandler.HandleAsync(
				new UpdateDaySlotCommand(
					ownerUserId,
					week,
					DayOfWeek.Monday,
					MealCategory.Breakfast,
					[new MealSlotItem(Option<string>.Some("11111111-1111-1111-1111-111111111111"), "Pasta", 2)]),
				TestContext.Current.CancellationToken);

			Assert.True(updateResult.IsSuccess);
		}

		using (var copyContext = TestDbContextFactory.CreateContext(databaseName: databaseName))
		{
			var copyHandler = new CopyCategoryCommandHandler(copyContext);
			var copyResult = await copyHandler.HandleAsync(
				new CopyCategoryCommand(
					ownerUserId,
					week,
					DayOfWeek.Monday,
					MealCategory.Breakfast,
					[DayOfWeek.Tuesday, DayOfWeek.Wednesday]),
				TestContext.Current.CancellationToken);

			Assert.True(copyResult.IsSuccess);
		}

		using (var generateContext = TestDbContextFactory.CreateContext(databaseName: databaseName))
		{
			var generateHandler = new GenerateGroceryListCommandHandler(generateContext);
			var generateResult = await generateHandler.HandleAsync(
				new GenerateGroceryListCommand(ownerUserId, week),
				TestContext.Current.CancellationToken);

			Assert.True(generateResult.IsSuccess);
		}

		using var verifyContext = TestDbContextFactory.CreateContext(databaseName: databaseName);
		var ownerItems = verifyContext.GroceryLists.Single(g => g.UserId == ownerUserId && g.WeekStart == "2026-02-23").Items;
		Assert.Contains(ownerItems, i =>
			i.Name == "Tomato"
			&& i.Unit == "pcs"
			&& decimal.Abs(i.Quantity - 3m) < 0.000001m);
		Assert.Contains(ownerItems, i =>
			i.Name == "Garlic"
			&& i.Unit == "cloves"
			&& decimal.Abs(i.Quantity - 6m) < 0.000001m);
		Assert.DoesNotContain(verifyContext.GroceryLists, g => g.UserId == collaboratorUserId && g.WeekStart == "2026-02-23");
	}

	[Fact]
	public async Task Flow_UpdateSlot_ThenCopyCategory_ThenGenerate_PersistsServingsScaling()
	{
		const string databaseName = "grocery-flow-slot-copy-generate";
		var week = new DateOnly(2026, 2, 23);

		TestDbContextFactory.CreateContext(seed: db =>
		{
			db.MealPlans.Add(CreateEmptyMealPlanForFlow());
			db.Recipes.Add(CreateSixServingRecipe());
		}, databaseName: databaseName).Dispose();

		using (var updateContext = TestDbContextFactory.CreateContext(databaseName: databaseName))
		{
			var updateHandler = new UpdateDaySlotCommandHandler(updateContext);
			var updateResult = await updateHandler.HandleAsync(
				new UpdateDaySlotCommand(
					"u1",
					week,
					DayOfWeek.Monday,
					MealCategory.Breakfast,
					[new MealSlotItem(Option<string>.Some("11111111-1111-1111-1111-111111111111"), "Pasta", 2)]),
				TestContext.Current.CancellationToken);

			Assert.True(updateResult.IsSuccess);
		}

		using (var copyContext = TestDbContextFactory.CreateContext(databaseName: databaseName))
		{
			var copyHandler = new CopyCategoryCommandHandler(copyContext);
			var copyResult = await copyHandler.HandleAsync(
				new CopyCategoryCommand(
					"u1",
					week,
					DayOfWeek.Monday,
					MealCategory.Breakfast,
					[DayOfWeek.Tuesday, DayOfWeek.Wednesday]),
				TestContext.Current.CancellationToken);

			Assert.True(copyResult.IsSuccess);
		}

		using (var generateContext = TestDbContextFactory.CreateContext(databaseName: databaseName))
		{
			var generateHandler = new GenerateGroceryListCommandHandler(generateContext);
			var generateResult = await generateHandler.HandleAsync(
				new GenerateGroceryListCommand("u1", week),
				TestContext.Current.CancellationToken);

			Assert.True(generateResult.IsSuccess);
		}

		using var verifyContext = TestDbContextFactory.CreateContext(databaseName: databaseName);
		var items = verifyContext.GroceryLists.Single(g => g.UserId == "u1" && g.WeekStart == "2026-02-23").Items;
		Assert.Contains(items, i =>
			i.Name == "Tomato"
			&& i.Unit == "pcs"
			&& decimal.Abs(i.Quantity - 3m) < 0.000001m);
		Assert.Contains(items, i =>
			i.Name == "Garlic"
			&& i.Unit == "cloves"
			&& decimal.Abs(i.Quantity - 6m) < 0.000001m);
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
