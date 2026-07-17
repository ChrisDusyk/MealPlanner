using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Commands;

public class PromotePantryStapleItemTests
{
	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenListMissing()
	{
		var handler = new PromotePantryStapleItemCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new PromotePantryStapleItemCommand(TestIds.Family("u1"), new DateOnly(2026, 2, 23), 0), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenIndexOutOfRange()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.GroceryLists.Add(new GroceryListEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = TestIds.Family("u1"),
				WeekStart = "2026-02-23",
				Items = [],
				PantryStapleItems = [new GroceryListItemData { Name = "Salt", Quantity = 1, Unit = "tsp", IsChecked = false, SourceRecipeNames = ["Pasta"] }],
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
		});

		var handler = new PromotePantryStapleItemCommandHandler(context);
		var result = await handler.HandleAsync(new PromotePantryStapleItemCommand(TestIds.Family("u1"), new DateOnly(2026, 2, 23), 5), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenIndexNegative()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.GroceryLists.Add(new GroceryListEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = TestIds.Family("u1"),
				WeekStart = "2026-02-23",
				Items = [],
				PantryStapleItems = [new GroceryListItemData { Name = "Salt", Quantity = 1, Unit = "tsp", IsChecked = false, SourceRecipeNames = [] }],
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
		});

		var handler = new PromotePantryStapleItemCommandHandler(context);
		var result = await handler.HandleAsync(new PromotePantryStapleItemCommand(TestIds.Family("u1"), new DateOnly(2026, 2, 23), -1), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_MovesItemToMainList_WhenIndexValid()
	{
		var id = Guid.NewGuid();
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.GroceryLists.Add(new GroceryListEntity
			{
				Id = id,
				FamilyGroupId = TestIds.Family("u1"),
				WeekStart = "2026-02-23",
				Items = [new GroceryListItemData { Name = "Tomato", Quantity = 2, Unit = "pcs", IsChecked = false, SourceRecipeNames = ["Pasta"] }],
				PantryStapleItems =
				[
					new GroceryListItemData { Name = "Salt", Quantity = 1, Unit = "tsp", IsChecked = false, SourceRecipeNames = ["Pasta"] },
					new GroceryListItemData { Name = "Olive Oil", Quantity = 2, Unit = "tbsp", IsChecked = false, SourceRecipeNames = ["Pasta"] }
				],
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
		});

		var handler = new PromotePantryStapleItemCommandHandler(context);
		var result = await handler.HandleAsync(new PromotePantryStapleItemCommand(TestIds.Family("u1"), new DateOnly(2026, 2, 23), 0), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		var entity = await context.GroceryLists.FindAsync([id], TestContext.Current.CancellationToken);
		Assert.NotNull(entity);
		Assert.Equal(2, entity.Items.Count);
		Assert.Contains(entity.Items, i => i.Name == "Tomato");
		Assert.Contains(entity.Items, i => i.Name == "Salt" && !i.IsChecked);
		Assert.Single(entity.PantryStapleItems);
		Assert.Equal("Olive Oil", entity.PantryStapleItems[0].Name);
		Assert.NotNull(result.Value);
		Assert.Equal(2, result.Value.Items.Count);
		Assert.Single(result.Value.PantryStapleItems);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new PromotePantryStapleItemCommandHandler(context);
		var result = await handler.HandleAsync(new PromotePantryStapleItemCommand(TestIds.Family("u1"), new DateOnly(2026, 2, 23), 0), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
