using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.Recipes.Models;
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
		var mealCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanDocument>)Array.Empty<MealPlanDocument>());
		var mealPlans = new Mock<IMongoCollection<MealPlanDocument>>();
		mealPlans.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(mealCursor.Object);
		mealPlans.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).Returns(mealCursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(mealPlans.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GenerateGroceryListCommandHandler(client.Object);
		var result = await handler.HandleAsync(new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_InsertsNewList_WhenNoExistingList()
	{
		var mealCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanDocument>)new List<MealPlanDocument> { CreateMealPlan() });
		var recipeCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<RecipeDocument>)new List<RecipeDocument> { CreateRecipe() });
		var emptyListCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<GroceryListDocument>)Array.Empty<GroceryListDocument>());

		var mealPlans = new Mock<IMongoCollection<MealPlanDocument>>();
		mealPlans.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(mealCursor.Object);
		mealPlans.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).Returns(mealCursor.Object);

		var recipes = new Mock<IMongoCollection<RecipeDocument>>();
		recipes.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<RecipeDocument>>(), It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(recipeCursor.Object);
		recipes.Setup(c => c.FindSync(It.IsAny<FilterDefinition<RecipeDocument>>(), It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>())).Returns(recipeCursor.Object);

		GroceryListDocument? inserted = null;
		var groceries = new Mock<IMongoCollection<GroceryListDocument>>();
		groceries.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(emptyListCursor.Object);
		groceries.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).Returns(emptyListCursor.Object);
		groceries.Setup(c => c.InsertOneAsync(It.IsAny<GroceryListDocument>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
			.Callback<GroceryListDocument, InsertOneOptions, CancellationToken>((d, _, _) => inserted = d)
			.Returns(Task.CompletedTask);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(mealPlans.Object);
		db.Setup(d => d.GetCollection<RecipeDocument>("recipes", null)).Returns(recipes.Object);
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(groceries.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GenerateGroceryListCommandHandler(client.Object);
		var result = await handler.HandleAsync(new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(inserted);
		Assert.Equal("u1", inserted.UserId);
		Assert.Contains(inserted.Items, i => i.Name == "Tomato" && i.Quantity == 3 && i.Unit == "pcs");
		Assert.Equal(1, inserted.Items.Count(i => i.Name.Equals("Bananas", StringComparison.OrdinalIgnoreCase)));
	}

	[Fact]
	public async Task HandleAsync_ReplacesExistingList_WhenPresent()
	{
		var existing = new GroceryListDocument { Id = "g1", UserId = "u1", WeekStart = "2026-02-23", Items = [], CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
		var mealCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanDocument>)new List<MealPlanDocument> { CreateMealPlan() });
		var recipeCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<RecipeDocument>)new List<RecipeDocument> { CreateRecipe() });
		var existingCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<GroceryListDocument>)new List<GroceryListDocument> { existing });

		var mealPlans = new Mock<IMongoCollection<MealPlanDocument>>();
		mealPlans.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(mealCursor.Object);
		mealPlans.Setup(c => c.FindSync(It.IsAny<FilterDefinition<MealPlanDocument>>(), It.IsAny<FindOptions<MealPlanDocument, MealPlanDocument>>(), It.IsAny<CancellationToken>())).Returns(mealCursor.Object);

		var recipes = new Mock<IMongoCollection<RecipeDocument>>();
		recipes.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<RecipeDocument>>(), It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(recipeCursor.Object);
		recipes.Setup(c => c.FindSync(It.IsAny<FilterDefinition<RecipeDocument>>(), It.IsAny<FindOptions<RecipeDocument, RecipeDocument>>(), It.IsAny<CancellationToken>())).Returns(recipeCursor.Object);

		GroceryListDocument? replaced = null;
		var groceries = new Mock<IMongoCollection<GroceryListDocument>>();
		groceries.Setup(c => c.FindAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(existingCursor.Object);
		groceries.Setup(c => c.FindSync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<FindOptions<GroceryListDocument, GroceryListDocument>>(), It.IsAny<CancellationToken>())).Returns(existingCursor.Object);
		groceries.Setup(c => c.ReplaceOneAsync(It.IsAny<FilterDefinition<GroceryListDocument>>(), It.IsAny<GroceryListDocument>(), It.IsAny<ReplaceOptions>(), It.IsAny<CancellationToken>()))
			.Callback<FilterDefinition<GroceryListDocument>, GroceryListDocument, ReplaceOptions, CancellationToken>((_, d, _, _) => replaced = d)
			.ReturnsAsync(Mock.Of<ReplaceOneResult>());

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanDocument>("mealplans", null)).Returns(mealPlans.Object);
		db.Setup(d => d.GetCollection<RecipeDocument>("recipes", null)).Returns(recipes.Object);
		db.Setup(d => d.GetCollection<GroceryListDocument>("grocerylists", null)).Returns(groceries.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new GenerateGroceryListCommandHandler(client.Object);
		var result = await handler.HandleAsync(new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(replaced);
		Assert.Equal("g1", replaced.Id);
		Assert.NotEmpty(replaced.Items);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));

		var handler = new GenerateGroceryListCommandHandler(client.Object);
		var result = await handler.HandleAsync(new GenerateGroceryListCommand("u1", new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
