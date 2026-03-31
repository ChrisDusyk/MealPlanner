using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Commands;

public class AddCustomItemTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenItemNameEmpty()
	{
		var handler = new AddCustomItemCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new AddCustomItemCommand("u1", new DateOnly(2026, 2, 23), "  "),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenListMissing()
	{
		var handler = new AddCustomItemCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new AddCustomItemCommand("u1", new DateOnly(2026, 2, 23), "Milk"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_AddsTrimmedCustomItem_WhenListExists()
	{
		var id = Guid.NewGuid();
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.GroceryLists.Add(new GroceryListEntity
			{
				Id = id,
				UserId = "u1",
				WeekStart = "2026-02-23",
				Items = [],
				PantryStapleItems = [],
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
		});

		var handler = new AddCustomItemCommandHandler(context);
		var result = await handler.HandleAsync(new AddCustomItemCommand("u1", new DateOnly(2026, 2, 23), "  Milk  "),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		var entity = await context.GroceryLists.FindAsync([id], TestContext.Current.CancellationToken);
		Assert.NotNull(entity);
		Assert.Contains(entity.Items, i => i.Name == "Milk" && i.Quantity == 0 && i.Unit == string.Empty);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new AddCustomItemCommandHandler(context);
		var result = await handler.HandleAsync(new AddCustomItemCommand("u1", new DateOnly(2026, 2, 23), "Milk"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_AddsCustomItem_WhenSharedWithReadWritePermission()
	{
		var listId = Guid.NewGuid();
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.GroceryLists.Add(new GroceryListEntity
			{
				Id = listId,
				UserId = "owner1",
				WeekStart = "2026-02-23",
				Items = [],
				PantryStapleItems = [],
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
			db.GroceryListShares.Add(new GroceryListShareEntity
			{
				Id = Guid.NewGuid(),
				OwnerUserId = "owner1",
				SharedWithUserId = "guest1",
				WeekStart = "2026-02-23",
				Permission = SharePermission.ReadWrite.ToString(),
				DismissedByRecipient = false,
				SharedAt = DateTime.UtcNow
			});
		});

		var handler = new AddCustomItemCommandHandler(context);
		var result = await handler.HandleAsync(
			new AddCustomItemCommand("guest1", new DateOnly(2026, 2, 23), "Bread", "owner1"),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		var entity = await context.GroceryLists.FindAsync([listId], TestContext.Current.CancellationToken);
		Assert.NotNull(entity);
		Assert.Contains(entity.Items, i => i.Name == "Bread");
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenSharedPermissionIsReadOnly()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.GroceryListShares.Add(new GroceryListShareEntity
			{
				Id = Guid.NewGuid(),
				OwnerUserId = "owner1",
				SharedWithUserId = "guest1",
				WeekStart = "2026-02-23",
				Permission = SharePermission.ReadOnly.ToString(),
				DismissedByRecipient = false,
				SharedAt = DateTime.UtcNow
			});
		});

		var handler = new AddCustomItemCommandHandler(context);
		var result = await handler.HandleAsync(
			new AddCustomItemCommand("guest1", new DateOnly(2026, 2, 23), "Bread", "owner1"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}
}
