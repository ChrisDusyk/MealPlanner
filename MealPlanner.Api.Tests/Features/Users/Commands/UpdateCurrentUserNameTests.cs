using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.Users.Commands;

public class UpdateCurrentUserNameTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenAuth0IdMissing()
	{
		var handler = new UpdateCurrentUserNameCommandHandler(new Mock<IMongoClient>().Object);
		var command = new UpdateCurrentUserNameCommand(" ", "Pat");

		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenNameMissing()
	{
		var handler = new UpdateCurrentUserNameCommandHandler(new Mock<IMongoClient>().Object);
		var command = new UpdateCurrentUserNameCommand("auth0|123", " ");

		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsUser_WhenUpdateSucceeds()
	{
		var doc = new UserDocument
		{
			Id = "u1",
			Auth0UserId = "auth0|123",
			Name = "Updated Name",
			Email = "pat@example.com",
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
		};

		var collection = new Mock<IMongoCollection<UserDocument>>();
		collection.Setup(c => c.FindOneAndUpdateAsync(
			It.IsAny<FilterDefinition<UserDocument>>(),
			It.IsAny<UpdateDefinition<UserDocument>>(),
			It.IsAny<FindOneAndUpdateOptions<UserDocument>>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync(doc);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(collection.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new UpdateCurrentUserNameCommandHandler(client.Object);
		var result = await handler.HandleAsync(
			new UpdateCurrentUserNameCommand("auth0|123", "Updated Name"),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal("u1", result.Value.Id);
		Assert.Equal("Updated Name", result.Value.Name);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenUserMissing()
	{
		var collection = new Mock<IMongoCollection<UserDocument>>();
		collection.Setup(c => c.FindOneAndUpdateAsync(
			It.IsAny<FilterDefinition<UserDocument>>(),
			It.IsAny<UpdateDefinition<UserDocument>>(),
			It.IsAny<FindOneAndUpdateOptions<UserDocument>>(),
			It.IsAny<CancellationToken>()))
			.Returns(Task.FromResult<UserDocument>(null!));

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(collection.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new UpdateCurrentUserNameCommandHandler(client.Object);
		var result = await handler.HandleAsync(
			new UpdateCurrentUserNameCommand("auth0|123", "Updated Name"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.NotFound, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var collection = new Mock<IMongoCollection<UserDocument>>();
		collection.Setup(c => c.FindOneAndUpdateAsync(
			It.IsAny<FilterDefinition<UserDocument>>(),
			It.IsAny<UpdateDefinition<UserDocument>>(),
			It.IsAny<FindOneAndUpdateOptions<UserDocument>>(),
			It.IsAny<CancellationToken>()))
			.ThrowsAsync(new Exception("update failed"));

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(collection.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new UpdateCurrentUserNameCommandHandler(client.Object);
		var result = await handler.HandleAsync(
			new UpdateCurrentUserNameCommand("auth0|123", "Updated Name"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error.Code);
	}
}
