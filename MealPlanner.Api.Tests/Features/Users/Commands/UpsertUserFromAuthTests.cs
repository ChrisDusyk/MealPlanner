using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.Users.Commands;

public class UpsertUserFromAuthTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenAuth0IdMissing()
	{
		var handler = new UpsertUserFromAuthCommandHandler(new Mock<IMongoClient>().Object);
		var command = new UpsertUserFromAuthCommand(" ", "Pat", Option<string>.Some("pat@example.com"));

		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenNameMissing()
	{
		var handler = new UpsertUserFromAuthCommandHandler(new Mock<IMongoClient>().Object);
		var command = new UpsertUserFromAuthCommand("auth0|123", " ", Option<string>.Some("pat@example.com"));

		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsUser_WhenUpsertSucceeds()
	{
		var doc = new UserDocument
		{
			Id = "u1",
			Auth0UserId = "auth0|123",
			Name = "Pat",
			Email = "pat@example.com",
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
		};

		var indexManager = new Mock<IMongoIndexManager<UserDocument>>();
		indexManager.Setup(i => i.CreateOneAsync(
			It.IsAny<CreateIndexModel<UserDocument>>(),
			It.IsAny<CreateOneIndexOptions>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync("ux_users_auth0UserId");

		var collection = new Mock<IMongoCollection<UserDocument>>();
		collection.SetupGet(c => c.Indexes).Returns(indexManager.Object);
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

		var handler = new UpsertUserFromAuthCommandHandler(client.Object);
		var command = new UpsertUserFromAuthCommand("auth0|123", "Pat", Option<string>.Some("pat@example.com"));
		var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal("u1", result.Value.Id);
		Assert.True(result.Value.Email.HasValue);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenUpsertReturnsNull()
	{
		var indexManager = new Mock<IMongoIndexManager<UserDocument>>();
		indexManager.Setup(i => i.CreateOneAsync(
			It.IsAny<CreateIndexModel<UserDocument>>(),
			It.IsAny<CreateOneIndexOptions>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync("ux_users_auth0UserId");

		var collection = new Mock<IMongoCollection<UserDocument>>();
		collection.SetupGet(c => c.Indexes).Returns(indexManager.Object);
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

		var handler = new UpsertUserFromAuthCommandHandler(client.Object);
		var result = await handler.HandleAsync(
			new UpsertUserFromAuthCommand("auth0|123", "Pat", Option<string>.None()),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var indexManager = new Mock<IMongoIndexManager<UserDocument>>();
		indexManager.Setup(i => i.CreateOneAsync(
			It.IsAny<CreateIndexModel<UserDocument>>(),
			It.IsAny<CreateOneIndexOptions>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync("ux_users_auth0UserId");

		var collection = new Mock<IMongoCollection<UserDocument>>();
		collection.SetupGet(c => c.Indexes).Returns(indexManager.Object);
		collection.Setup(c => c.FindOneAndUpdateAsync(
			It.IsAny<FilterDefinition<UserDocument>>(),
			It.IsAny<UpdateDefinition<UserDocument>>(),
			It.IsAny<FindOneAndUpdateOptions<UserDocument>>(),
			It.IsAny<CancellationToken>()))
			.ThrowsAsync(new Exception("upsert failed"));

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(collection.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new UpsertUserFromAuthCommandHandler(client.Object);
		var result = await handler.HandleAsync(
			new UpsertUserFromAuthCommand("auth0|123", "Pat", Option<string>.Some("pat@example.com")),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error.Code);
	}
}
