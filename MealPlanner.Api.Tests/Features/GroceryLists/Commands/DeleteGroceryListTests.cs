using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Commands;

public class DeleteGroceryListTests
{
	[Fact]
	public async Task HandleAsync_ReturnsSuccess_WhenDeleted()
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

		var handler = new DeleteGroceryListCommandHandler(context);
		var result = await handler.HandleAsync(new DeleteGroceryListCommand("u1", new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Empty(context.GroceryLists);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenNothingDeleted()
	{
		var handler = new DeleteGroceryListCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new DeleteGroceryListCommand("u1", new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new DeleteGroceryListCommandHandler(context);
		var result = await handler.HandleAsync(new DeleteGroceryListCommand("u1", new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
