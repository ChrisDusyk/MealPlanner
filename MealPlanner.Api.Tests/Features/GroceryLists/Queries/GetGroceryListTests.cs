using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Queries;

public class GetGroceryListTests
{
	[Fact]
	public async Task HandleAsync_ReturnsList_WhenFound()
	{
		var id = Guid.NewGuid();
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.GroceryLists.Add(new GroceryListEntity
			{
				Id = id,
				FamilyGroupId = TestIds.Family("u1"),
				WeekStart = "2026-02-23",
				Items = [new GroceryListItemData { Name = "Rice", Quantity = 1, Unit = "kg", IsChecked = false, SourceRecipeNames = [] }],
				PantryStapleItems = [],
				CreatedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
				UpdatedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc)
			});
		});

		var handler = new GetGroceryListQueryHandler(context);
		var result = await handler.HandleAsync(new GetGroceryListQuery(TestIds.Family("u1"), new DateOnly(2026, 2, 25)), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal(id.ToString(), result.Value.Id);
		Assert.Equal(new DateOnly(2026, 2, 23), result.Value.WeekStart);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenMissing()
	{
		var handler = new GetGroceryListQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new GetGroceryListQuery(TestIds.Family("u1"), new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.NotFound, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new GetGroceryListQueryHandler(context);
		var result = await handler.HandleAsync(new GetGroceryListQuery(TestIds.Family("u1"), new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
