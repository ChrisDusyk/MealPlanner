using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Features.Users.Queries;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.Users.Queries;

public class FindUserByEmailTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenEmailMissing()
	{
		var handler = new FindUserByEmailQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new FindUserByEmailQuery(" "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsUser_WhenFound()
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

		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<UserDocument>)new List<UserDocument> { doc });
		var collection = new Mock<IMongoCollection<UserDocument>>();
		collection.Setup(c => c.FindAsync(
			It.IsAny<FilterDefinition<UserDocument>>(),
			It.IsAny<FindOptions<UserDocument, UserDocument>>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(
			It.IsAny<FilterDefinition<UserDocument>>(),
			It.IsAny<FindOptions<UserDocument, UserDocument>>(),
			It.IsAny<CancellationToken>()))
			.Returns(cursor.Object);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(collection.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new FindUserByEmailQueryHandler(client.Object);
		var result = await handler.HandleAsync(new FindUserByEmailQuery("pat@example.com"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal("u1", result.Value.Id);
		Assert.True(result.Value.Email.HasValue);
		Assert.Equal("pat@example.com", result.Value.Email.Value);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenMissing()
	{
		var cursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<UserDocument>)Array.Empty<UserDocument>());
		var collection = new Mock<IMongoCollection<UserDocument>>();
		collection.Setup(c => c.FindAsync(
			It.IsAny<FilterDefinition<UserDocument>>(),
			It.IsAny<FindOptions<UserDocument, UserDocument>>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync(cursor.Object);
		collection.Setup(c => c.FindSync(
			It.IsAny<FilterDefinition<UserDocument>>(),
			It.IsAny<FindOptions<UserDocument, UserDocument>>(),
			It.IsAny<CancellationToken>()))
			.Returns(cursor.Object);

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<UserDocument>("users", null)).Returns(collection.Object);

		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new FindUserByEmailQueryHandler(client.Object);
		var result = await handler.HandleAsync(new FindUserByEmailQuery("missing@example.com"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.NotFound, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenMongoThrows()
	{
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Throws(new Exception("boom"));

		var handler = new FindUserByEmailQueryHandler(client.Object);
		var result = await handler.HandleAsync(new FindUserByEmailQuery("pat@example.com"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error.Code);
	}
}
