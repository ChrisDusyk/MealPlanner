using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Commands;

public class ToggleGroceryListItemTests
{
	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenListMissing()
	{
		var handler = new ToggleGroceryListItemCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new ToggleGroceryListItemCommand("u1", new DateOnly(2026, 2, 23), 0), TestContext.Current.CancellationToken);

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
				UserId = "u1",
				WeekStart = "2026-02-23",
				Items = [],
				PantryStapleItems = [],
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
		});

		var handler = new ToggleGroceryListItemCommandHandler(context);
		var result = await handler.HandleAsync(new ToggleGroceryListItemCommand("u1", new DateOnly(2026, 2, 23), 5), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_TogglesItem_WhenIndexValid()
	{
		var id = Guid.NewGuid();
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.GroceryLists.Add(new GroceryListEntity
			{
				Id = id,
				UserId = "u1",
				WeekStart = "2026-02-23",
				Items = [new GroceryListItemData { Name = "Rice", Quantity = 1, Unit = "kg", IsChecked = false, SourceRecipeNames = [] }],
				PantryStapleItems = [],
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
		});

		var handler = new ToggleGroceryListItemCommandHandler(context);
		var result = await handler.HandleAsync(new ToggleGroceryListItemCommand("u1", new DateOnly(2026, 2, 23), 0), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		var entity = await context.GroceryLists.FindAsync([id], TestContext.Current.CancellationToken);
		Assert.NotNull(entity);
		Assert.True(entity.Items[0].IsChecked);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new ToggleGroceryListItemCommandHandler(context);
		var result = await handler.HandleAsync(new ToggleGroceryListItemCommand("u1", new DateOnly(2026, 2, 23), 0), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
