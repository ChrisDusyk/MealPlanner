using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Shared;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Commands;

public class DeleteGroceryListTests
{
	[Fact]
	public async Task HandleAsync_ReturnsSuccess_WhenDeleted()
	{
		var deleteResult = new Mock<DeleteResult>();
		deleteResult.SetupGet(r => r.DeletedCount).Returns(1);

		var collection = new Mock<IMongoCollection<MealPlanner.Api.Features.GroceryLists.Models.GroceryListDocument>>();
		collection.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<MealPlanner.Api.Features.GroceryLists.Models.GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(deleteResult.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanner.Api.Features.GroceryLists.Models.GroceryListDocument>("grocerylists", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new DeleteGroceryListCommandHandler(client.Object);
		var result = await handler.HandleAsync(new DeleteGroceryListCommand("u1", new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenNothingDeleted()
	{
		var deleteResult = new Mock<DeleteResult>();
		deleteResult.SetupGet(r => r.DeletedCount).Returns(0);

		var collection = new Mock<IMongoCollection<MealPlanner.Api.Features.GroceryLists.Models.GroceryListDocument>>();
		collection.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<MealPlanner.Api.Features.GroceryLists.Models.GroceryListDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(deleteResult.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanner.Api.Features.GroceryLists.Models.GroceryListDocument>("grocerylists", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new DeleteGroceryListCommandHandler(client.Object);
		var result = await handler.HandleAsync(new DeleteGroceryListCommand("u1", new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));

		var handler = new DeleteGroceryListCommandHandler(client.Object);
		var result = await handler.HandleAsync(new DeleteGroceryListCommand("u1", new DateOnly(2026, 2, 23)), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
