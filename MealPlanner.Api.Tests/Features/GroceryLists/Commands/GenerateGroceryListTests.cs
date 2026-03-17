using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Commands;

public class GenerateGroceryListTests
{
	private static MealPlanDocument CreateMealPlan()
	{
		return new MealPlanDocument
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
						["Dinner"] =
						[
							new MealSlotItemDocument { RecipeId = "r1", Name = "Pasta" },
							new MealSlotItemDocument { RecipeId = null, Name = "Bananas" },
							new MealSlotItemDocument { RecipeId = null, Name = "bananas" }
						]
					}
				}
			],
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
	}

	private static RecipeDocument CreateRecipe() => new()
	{
		Id = "r1",
		UserId = "u1",
		Name = "Pasta",
		Description = "",
		Ingredients =
		[
			new IngredientDocument { Name = "Tomato", Quantity = 2, Unit = "pcs" },
			new IngredientDocument { Name = "Tomato", Quantity = 1, Unit = "pcs" }
		],
		CreatedAt = DateTime.UtcNow,
		UpdatedAt = DateTime.UtcNow
	};

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenMealPlanMissing()
	{
		var mealCursor = MongoTestHelpers.CreateCursor(Array.Empty<MealPlanDocument>());
		var mealPlans = new Mock<IMongoCollection<MealPlanDocument>>();
		mealPlans.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mealCursor.Object);
		mealPlans.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(mealCursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(mealPlans.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GenerateGroceryListCommandHandler(client.Object);
		var result = await handler.HandleAsync(new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_InsertsNewList_WhenNoExistingList()
	{
		var mealCursor = MongoTestHelpers.CreateCursor(new List<MealPlanDocument> { CreateMealPlan() });
		var recipeCursor = MongoTestHelpers.CreateCursor(new List<RecipeDocument> { CreateRecipe() });
		var emptyListCursor = MongoTestHelpers.CreateCursor(Array.Empty<GroceryListDocument>());
		var emptyShareCursor = MongoTestHelpers.CreateCursor(Array.Empty<MealPlanShareDocument>());
		var emptyGroceryShareCursor = MongoTestHelpers.CreateCursor(Array.Empty<GroceryListShareDocument>());

		var mealPlans = new Mock<IMongoCollection<MealPlanDocument>>();
		mealPlans.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mealCursor.Object);
		mealPlans.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(mealCursor.Object);

		var recipes = new Mock<IMongoCollection<RecipeDocument>>();
		recipes.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<RecipeDocument>>(),
				It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(recipeCursor.Object);
		recipes.Setup(c => c.FindSync(It.IsAny<FilterDefinition<RecipeDocument>>(),
				It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(recipeCursor.Object);

		var shares = new Mock<IMongoCollection<MealPlanShareDocument>>();
		shares.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(),
				It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(emptyShareCursor.Object);
		shares.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(),
				It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(emptyShareCursor.Object);

		var groceryShares = new Mock<IMongoCollection<GroceryListShareDocument>>();
		groceryShares
			.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>())).ReturnsAsync(emptyGroceryShareCursor.Object);
		groceryShares
			.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>())).Returns(emptyGroceryShareCursor.Object);

		GroceryListDocument? inserted = null;
		var groceries = new Mock<IMongoCollection<GroceryListDocument>>();
		groceries.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(emptyListCursor.Object);
		groceries.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(emptyListCursor.Object);
		groceries.Setup(c => c.InsertOneAsync(It.IsAny<GroceryListDocument>(), It.IsAny<InsertOneOptions>(),
				It.IsAny<CancellationToken>()))
			.Callback<GroceryListDocument, InsertOneOptions, CancellationToken>((d, _, _) => inserted = d)
			.Returns(Task.CompletedTask);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(mealPlans.Object);
		db.Setup(d => d.GetCollection<RecipeDocument>("recipes", null)).Returns(recipes.Object);
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(groceries.Object);
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		db.Setup(d => d.GetCollection<GroceryListShareDocument>("grocerylist_shares", null))
			.Returns(groceryShares.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GenerateGroceryListCommandHandler(client.Object);
		var result = await handler.HandleAsync(new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(inserted);
		Assert.Equal("u1", inserted.UserId);
		Assert.Contains(inserted.Items, i => i.Name == "Tomato" && i.Quantity == 3 && i.Unit == "pcs");
		Assert.Equal(1, inserted.Items.Count(i => i.Name.Equals("Bananas", StringComparison.OrdinalIgnoreCase)));
		Assert.Empty(inserted.PantryStapleItems);
	}

	[Fact]
	public async Task HandleAsync_ReplacesExistingList_WhenPresent()
	{
		var existing = new GroceryListDocument
		{
			Id = "g1", UserId = "u1", WeekStart = "2026-02-23", Items = [], CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		var mealCursor = MongoTestHelpers.CreateCursor(new List<MealPlanDocument> { CreateMealPlan() });
		var recipeCursor = MongoTestHelpers.CreateCursor(new List<RecipeDocument> { CreateRecipe() });
		var existingCursor = MongoTestHelpers.CreateCursor(new List<GroceryListDocument> { existing });
		var emptyShareCursor = MongoTestHelpers.CreateCursor(Array.Empty<MealPlanShareDocument>());
		var emptyGroceryShareCursor = MongoTestHelpers.CreateCursor(Array.Empty<GroceryListShareDocument>());

		var mealPlans = new Mock<IMongoCollection<MealPlanDocument>>();
		mealPlans.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mealCursor.Object);
		mealPlans.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(mealCursor.Object);

		var recipes = new Mock<IMongoCollection<RecipeDocument>>();
		recipes.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<RecipeDocument>>(),
				It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(recipeCursor.Object);
		recipes.Setup(c => c.FindSync(It.IsAny<FilterDefinition<RecipeDocument>>(),
				It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(recipeCursor.Object);

		var shares = new Mock<IMongoCollection<MealPlanShareDocument>>();
		shares.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(),
				It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(emptyShareCursor.Object);
		shares.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(),
				It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(emptyShareCursor.Object);

		var groceryShares = new Mock<IMongoCollection<GroceryListShareDocument>>();
		groceryShares
			.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>())).ReturnsAsync(emptyGroceryShareCursor.Object);
		groceryShares
			.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>())).Returns(emptyGroceryShareCursor.Object);

		GroceryListDocument? replaced = null;
		var groceries = new Mock<IMongoCollection<GroceryListDocument>>();
		groceries.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(existingCursor.Object);
		groceries.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(existingCursor.Object);
		groceries.Setup(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<GroceryListDocument>(), It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
			.Callback<FilterDefinition<GroceryListDocument>, GroceryListDocument, ReplaceOptions, CancellationToken>((_,
				d, _, _) => replaced = d)
			.ReturnsAsync(Mock.Of<ReplaceOneResult>());

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(mealPlans.Object);
		db.Setup(d => d.GetCollection<RecipeDocument>("recipes", null)).Returns(recipes.Object);
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(groceries.Object);
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		db.Setup(d => d.GetCollection<GroceryListShareDocument>("grocerylist_shares", null))
			.Returns(groceryShares.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GenerateGroceryListCommandHandler(client.Object);
		var result = await handler.HandleAsync(new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(replaced);
		Assert.Equal("g1", replaced.Id);
		Assert.NotEmpty(replaced.Items);
		Assert.Empty(replaced.PantryStapleItems);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));

		var handler = new GenerateGroceryListCommandHandler(client.Object);
		var result = await handler.HandleAsync(new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ScalesIngredientsByServings()
	{
		// Recipe yields 4 servings; slot requests 2 servings → scaling factor = 0.5
		var mealPlan = new MealPlanDocument
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
						["Dinner"] = [new MealSlotItemDocument { RecipeId = "r1", Name = "Pasta", Servings = 2 }]
					}
				}
			],
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var recipe = new RecipeDocument
		{
			Id = "r1",
			UserId = "u1",
			Name = "Pasta",
			Description = "",
			Servings = 4,
			Ingredients =
			[
				new IngredientDocument { Name = "Tomato", Quantity = 8, Unit = "pcs" },
				new IngredientDocument { Name = "Pasta", Quantity = 400, Unit = "g" }
			],
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var mealCursor = MongoTestHelpers.CreateCursor(new List<MealPlanDocument> { mealPlan });
		var recipeCursor = MongoTestHelpers.CreateCursor(new List<RecipeDocument> { recipe });
		var emptyListCursor = MongoTestHelpers.CreateCursor(Array.Empty<GroceryListDocument>());
		var emptyShareCursor = MongoTestHelpers.CreateCursor(Array.Empty<MealPlanShareDocument>());
		var emptyGroceryShareCursor = MongoTestHelpers.CreateCursor(Array.Empty<GroceryListShareDocument>());

		var mealPlans = new Mock<IMongoCollection<MealPlanDocument>>();
		mealPlans.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mealCursor.Object);
		mealPlans.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(mealCursor.Object);

		var recipes = new Mock<IMongoCollection<RecipeDocument>>();
		recipes.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<RecipeDocument>>(),
				It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(recipeCursor.Object);
		recipes.Setup(c => c.FindSync(It.IsAny<FilterDefinition<RecipeDocument>>(),
				It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(recipeCursor.Object);

		var shares = new Mock<IMongoCollection<MealPlanShareDocument>>();
		shares.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(),
				It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(emptyShareCursor.Object);
		shares.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(),
				It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(emptyShareCursor.Object);

		var groceryShares = new Mock<IMongoCollection<GroceryListShareDocument>>();
		groceryShares
			.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>())).ReturnsAsync(emptyGroceryShareCursor.Object);
		groceryShares
			.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>())).Returns(emptyGroceryShareCursor.Object);

		GroceryListDocument? inserted = null;
		var groceries = new Mock<IMongoCollection<GroceryListDocument>>();
		groceries.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(emptyListCursor.Object);
		groceries.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(emptyListCursor.Object);
		groceries.Setup(c => c.InsertOneAsync(It.IsAny<GroceryListDocument>(), It.IsAny<InsertOneOptions>(),
				It.IsAny<CancellationToken>()))
			.Callback<GroceryListDocument, InsertOneOptions, CancellationToken>((d, _, _) => inserted = d)
			.Returns(Task.CompletedTask);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(mealPlans.Object);
		db.Setup(d => d.GetCollection<RecipeDocument>("recipes", null)).Returns(recipes.Object);
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(groceries.Object);
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		db.Setup(d => d.GetCollection<GroceryListShareDocument>("grocerylist_shares", null))
			.Returns(groceryShares.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GenerateGroceryListCommandHandler(client.Object);
		var result = await handler.HandleAsync(new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(inserted);
		// 8 tomatoes * (2/4) = 4, 400g pasta * (2/4) = 200
		Assert.Contains(inserted.Items, i => i.Name == "Tomato" && i.Quantity == 4m && i.Unit == "pcs");
		Assert.Contains(inserted.Items, i => i.Name == "Pasta" && i.Quantity == 200m && i.Unit == "g");
		Assert.Empty(inserted.PantryStapleItems);
	}

	[Fact]
	public async Task HandleAsync_SeparatesPantryStaplesFromRegularItems()
	{
		var mealPlan = new MealPlanDocument
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
						["Dinner"] = [new MealSlotItemDocument { RecipeId = "r1", Name = "Pasta" }]
					}
				}
			],
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var recipe = new RecipeDocument
		{
			Id = "r1",
			UserId = "u1",
			Name = "Pasta",
			Description = "",
			Ingredients =
			[
				new IngredientDocument { Name = "Tomato", Quantity = 2, Unit = "pcs", IsPantryStaple = false },
				new IngredientDocument { Name = "Salt", Quantity = 1, Unit = "tsp", IsPantryStaple = true },
				new IngredientDocument { Name = "Olive Oil", Quantity = 2, Unit = "tbsp", IsPantryStaple = true }
			],
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var mealCursor = MongoTestHelpers.CreateCursor(new List<MealPlanDocument> { mealPlan });
		var recipeCursor = MongoTestHelpers.CreateCursor(new List<RecipeDocument> { recipe });
		var emptyListCursor = MongoTestHelpers.CreateCursor(Array.Empty<GroceryListDocument>());
		var emptyShareCursor = MongoTestHelpers.CreateCursor(Array.Empty<MealPlanShareDocument>());
		var emptyGroceryShareCursor = MongoTestHelpers.CreateCursor(Array.Empty<GroceryListShareDocument>());

		var mealPlans = new Mock<IMongoCollection<MealPlanDocument>>();
		mealPlans.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mealCursor.Object);
		mealPlans.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(mealCursor.Object);

		var recipes = new Mock<IMongoCollection<RecipeDocument>>();
		recipes.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<RecipeDocument>>(),
				It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(recipeCursor.Object);
		recipes.Setup(c => c.FindSync(It.IsAny<FilterDefinition<RecipeDocument>>(),
				It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(recipeCursor.Object);

		var shares = new Mock<IMongoCollection<MealPlanShareDocument>>();
		shares.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(),
				It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(emptyShareCursor.Object);
		shares.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(),
				It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(emptyShareCursor.Object);

		var groceryShares = new Mock<IMongoCollection<GroceryListShareDocument>>();
		groceryShares
			.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>())).ReturnsAsync(emptyGroceryShareCursor.Object);
		groceryShares
			.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>())).Returns(emptyGroceryShareCursor.Object);

		GroceryListDocument? inserted = null;
		var groceries = new Mock<IMongoCollection<GroceryListDocument>>();
		groceries.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(emptyListCursor.Object);
		groceries.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(emptyListCursor.Object);
		groceries.Setup(c => c.InsertOneAsync(It.IsAny<GroceryListDocument>(), It.IsAny<InsertOneOptions>(),
				It.IsAny<CancellationToken>()))
			.Callback<GroceryListDocument, InsertOneOptions, CancellationToken>((d, _, _) => inserted = d)
			.Returns(Task.CompletedTask);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(mealPlans.Object);
		db.Setup(d => d.GetCollection<RecipeDocument>("recipes", null)).Returns(recipes.Object);
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(groceries.Object);
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		db.Setup(d => d.GetCollection<GroceryListShareDocument>("grocerylist_shares", null))
			.Returns(groceryShares.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GenerateGroceryListCommandHandler(client.Object);
		var result = await handler.HandleAsync(new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(inserted);
		// Regular item goes to Items
		Assert.Single(inserted.Items);
		Assert.Contains(inserted.Items, i => i.Name == "Tomato" && i.Quantity == 2 && i.Unit == "pcs");
		// Pantry staples go to PantryStapleItems
		Assert.Equal(2, inserted.PantryStapleItems.Count);
		Assert.Contains(inserted.PantryStapleItems, i => i.Name == "Salt" && i.Quantity == 1 && i.Unit == "tsp");
		Assert.Contains(inserted.PantryStapleItems, i => i.Name == "Olive Oil" && i.Quantity == 2 && i.Unit == "tbsp");
	}

	[Fact]
	public async Task HandleAsync_AutoSharesFromFriendPreferences_WhenEnabledAndFriendshipActive()
	{
		var mealCursor = MongoTestHelpers.CreateCursor(new List<MealPlanDocument> { CreateMealPlan() });
		var recipeCursor = MongoTestHelpers.CreateCursor(new List<RecipeDocument> { CreateRecipe() });
		var emptyListCursor = MongoTestHelpers.CreateCursor(Array.Empty<GroceryListDocument>());
		var emptyShareCursor = MongoTestHelpers.CreateCursor(Array.Empty<MealPlanShareDocument>());
		var emptyGroceryShareCursor = MongoTestHelpers.CreateCursor(Array.Empty<GroceryListShareDocument>());

		var mealPlans = new Mock<IMongoCollection<MealPlanDocument>>();
		mealPlans.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mealCursor.Object);
		mealPlans.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(mealCursor.Object);

		var recipes = new Mock<IMongoCollection<RecipeDocument>>();
		recipes.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<RecipeDocument>>(),
				It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(recipeCursor.Object);
		recipes.Setup(c => c.FindSync(It.IsAny<FilterDefinition<RecipeDocument>>(),
				It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(recipeCursor.Object);

		var shares = new Mock<IMongoCollection<MealPlanShareDocument>>();
		shares.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(),
				It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(emptyShareCursor.Object);
		shares.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(),
				It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(emptyShareCursor.Object);

		var preferencesCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<FriendAutoSharePreferenceDocument>)
		[
			new FriendAutoSharePreferenceDocument
			{
				UserId = "u1",
				FriendUserId = "u2",
				AutoShareMealPlans = false,
				AutoShareGroceryLists = true
			}
		]);
		var preferences = new Mock<IMongoCollection<FriendAutoSharePreferenceDocument>>();
		preferences.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<FriendAutoSharePreferenceDocument>>(),
				It.IsAny<FindOptions<FriendAutoSharePreferenceDocument, FriendAutoSharePreferenceDocument>>(),
				It.IsAny<CancellationToken>())).ReturnsAsync(preferencesCursor.Object);
		preferences.Setup(c => c.FindSync(It.IsAny<FilterDefinition<FriendAutoSharePreferenceDocument>>(),
				It.IsAny<FindOptions<FriendAutoSharePreferenceDocument, FriendAutoSharePreferenceDocument>>(),
				It.IsAny<CancellationToken>())).Returns(preferencesCursor.Object);

		var friendshipsCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<FriendshipDocument>)
		[
			new FriendshipDocument
			{
				UserAId = "u1",
				UserBId = "u2",
				CreatedAt = DateTime.UtcNow
			}
		]);
		var friendships = new Mock<IMongoCollection<FriendshipDocument>>();
		friendships.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<FriendshipDocument>>(),
				It.IsAny<FindOptions<FriendshipDocument, FriendshipDocument>>(),
				It.IsAny<CancellationToken>())).ReturnsAsync(friendshipsCursor.Object);
		friendships.Setup(c => c.FindSync(It.IsAny<FilterDefinition<FriendshipDocument>>(),
				It.IsAny<FindOptions<FriendshipDocument, FriendshipDocument>>(),
				It.IsAny<CancellationToken>())).Returns(friendshipsCursor.Object);

		var groceryShares = new Mock<IMongoCollection<GroceryListShareDocument>>();
		groceryShares
			.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>())).ReturnsAsync(emptyGroceryShareCursor.Object);
		groceryShares
			.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>())).Returns(emptyGroceryShareCursor.Object);
		groceryShares
			.Setup(c => c.InsertManyAsync(It.IsAny<IEnumerable<GroceryListShareDocument>>(),
				It.IsAny<InsertManyOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

		var groceries = new Mock<IMongoCollection<GroceryListDocument>>();
		groceries.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(emptyListCursor.Object);
		groceries.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(emptyListCursor.Object);
		groceries.Setup(c => c.InsertOneAsync(It.IsAny<GroceryListDocument>(), It.IsAny<InsertOneOptions>(),
				It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(mealPlans.Object);
		db.Setup(d => d.GetCollection<RecipeDocument>("recipes", null)).Returns(recipes.Object);
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(groceries.Object);
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		db.Setup(d => d.GetCollection<FriendAutoSharePreferenceDocument>("friend_auto_share_preferences", null)).Returns(preferences.Object);
		db.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);
		db.Setup(d => d.GetCollection<GroceryListShareDocument>("grocerylist_shares", null)).Returns(groceryShares.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GenerateGroceryListCommandHandler(client.Object);
		var result = await handler.HandleAsync(new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		groceryShares.Verify(c => c.InsertManyAsync(
			It.Is<IEnumerable<GroceryListShareDocument>>(items => items.Count() == 1 && items.First().SharedWithUserId == "u2"),
			It.IsAny<InsertManyOptions>(),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task HandleAsync_DoesNotDuplicateFriendAutoShares_WhenExistingSharePresent()
	{
		var mealCursor = MongoTestHelpers.CreateCursor(new List<MealPlanDocument> { CreateMealPlan() });
		var recipeCursor = MongoTestHelpers.CreateCursor(new List<RecipeDocument> { CreateRecipe() });
		var emptyListCursor = MongoTestHelpers.CreateCursor(Array.Empty<GroceryListDocument>());
		var emptyShareCursor = MongoTestHelpers.CreateCursor(Array.Empty<MealPlanShareDocument>());
		var existingGroceryShareCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<GroceryListShareDocument>)
		[
			new GroceryListShareDocument
			{
				OwnerUserId = "u1",
				SharedWithUserId = "u2",
				WeekStart = "2026-02-23",
				Permission = nameof(SharePermission.ReadWrite),
				SharedAt = DateTime.UtcNow,
				DismissedByRecipient = false
			}
		]);

		var mealPlans = new Mock<IMongoCollection<MealPlanDocument>>();
		mealPlans.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(mealCursor.Object);
		mealPlans.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(),
				It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(mealCursor.Object);

		var recipes = new Mock<IMongoCollection<RecipeDocument>>();
		recipes.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<RecipeDocument>>(),
				It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(recipeCursor.Object);
		recipes.Setup(c => c.FindSync(It.IsAny<FilterDefinition<RecipeDocument>>(),
				It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(recipeCursor.Object);

		var shares = new Mock<IMongoCollection<MealPlanShareDocument>>();
		shares.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(),
				It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(emptyShareCursor.Object);
		shares.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(),
				It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(emptyShareCursor.Object);

		var preferencesCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<FriendAutoSharePreferenceDocument>)
		[
			new FriendAutoSharePreferenceDocument
			{
				UserId = "u1",
				FriendUserId = "u2",
				AutoShareMealPlans = false,
				AutoShareGroceryLists = true
			}
		]);
		var preferences = new Mock<IMongoCollection<FriendAutoSharePreferenceDocument>>();
		preferences.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<FriendAutoSharePreferenceDocument>>(),
				It.IsAny<FindOptions<FriendAutoSharePreferenceDocument, FriendAutoSharePreferenceDocument>>(),
				It.IsAny<CancellationToken>())).ReturnsAsync(preferencesCursor.Object);
		preferences.Setup(c => c.FindSync(It.IsAny<FilterDefinition<FriendAutoSharePreferenceDocument>>(),
				It.IsAny<FindOptions<FriendAutoSharePreferenceDocument, FriendAutoSharePreferenceDocument>>(),
				It.IsAny<CancellationToken>())).Returns(preferencesCursor.Object);

		var friendshipsCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<FriendshipDocument>)
		[
			new FriendshipDocument
			{
				UserAId = "u1",
				UserBId = "u2",
				CreatedAt = DateTime.UtcNow
			}
		]);
		var friendships = new Mock<IMongoCollection<FriendshipDocument>>();
		friendships.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<FriendshipDocument>>(),
				It.IsAny<FindOptions<FriendshipDocument, FriendshipDocument>>(),
				It.IsAny<CancellationToken>())).ReturnsAsync(friendshipsCursor.Object);
		friendships.Setup(c => c.FindSync(It.IsAny<FilterDefinition<FriendshipDocument>>(),
				It.IsAny<FindOptions<FriendshipDocument, FriendshipDocument>>(),
				It.IsAny<CancellationToken>())).Returns(friendshipsCursor.Object);

		var groceryShares = new Mock<IMongoCollection<GroceryListShareDocument>>();
		groceryShares
			.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>())).ReturnsAsync(existingGroceryShareCursor.Object);
		groceryShares
			.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>())).Returns(existingGroceryShareCursor.Object);

		var groceries = new Mock<IMongoCollection<GroceryListDocument>>();
		groceries.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(emptyListCursor.Object);
		groceries.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(),
				It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.Returns(emptyListCursor.Object);
		groceries.Setup(c => c.InsertOneAsync(It.IsAny<GroceryListDocument>(), It.IsAny<InsertOneOptions>(),
				It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(mealPlans.Object);
		db.Setup(d => d.GetCollection<RecipeDocument>("recipes", null)).Returns(recipes.Object);
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(groceries.Object);
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(shares.Object);
		db.Setup(d => d.GetCollection<FriendAutoSharePreferenceDocument>("friend_auto_share_preferences", null)).Returns(preferences.Object);
		db.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);
		db.Setup(d => d.GetCollection<GroceryListShareDocument>("grocerylist_shares", null)).Returns(groceryShares.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GenerateGroceryListCommandHandler(client.Object);
		var result = await handler.HandleAsync(new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		groceryShares.Verify(c => c.InsertManyAsync(
			It.IsAny<IEnumerable<GroceryListShareDocument>>(),
			It.IsAny<InsertManyOptions>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}
}
