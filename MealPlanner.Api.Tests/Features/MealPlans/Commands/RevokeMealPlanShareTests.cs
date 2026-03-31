using MealPlanner.Api.Features.MealPlans.Commands;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Shared;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.MealPlans.Commands;

public class RevokeMealPlanShareTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenShareIdMissing()
	{
		var handler = new RevokeMealPlanShareCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new RevokeMealPlanShareCommand("owner1", " "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenNothingDeleted()
	{
		var deleteResult = new Mock<DeleteResult>();
		deleteResult.SetupGet(r => r.DeletedCount).Returns(0);
		var collection = new Mock<IMongoCollection<MealPlanShareDocument>>();
		collection.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(deleteResult.Object);
		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new RevokeMealPlanShareCommandHandler(client.Object);
		var result = await handler.HandleAsync(new RevokeMealPlanShareCommand("owner1", "s1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsSuccess_WhenDeleted()
	{
		var deleteResult = new Mock<DeleteResult>();
		deleteResult.SetupGet(r => r.DeletedCount).Returns(1);
		var collection = new Mock<IMongoCollection<MealPlanShareDocument>>();
		collection.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<MealPlanShareDocument>>(), It.IsAny<CancellationToken>())).ReturnsAsync(deleteResult.Object);
		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null)).Returns(collection.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

		var handler = new RevokeMealPlanShareCommandHandler(client.Object);
		var result = await handler.HandleAsync(new RevokeMealPlanShareCommand("owner1", "s1"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));
		var handler = new RevokeMealPlanShareCommandHandler(client.Object);

		var result = await handler.HandleAsync(new RevokeMealPlanShareCommand("owner1", "s1"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
