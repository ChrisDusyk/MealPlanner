using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Shared;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.Users.Commands;

public class RemoveFriendTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenFriendMissing()
	{
		var handler = new RemoveFriendCommandHandler(new Mock<IMongoClient>().Object);
		var result = await handler.HandleAsync(new RemoveFriendCommand("auth0|me", " "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenFriendshipMissing()
	{
		var friendships = new Mock<IMongoCollection<FriendshipDocument>>();
		friendships.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<FriendshipDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new DeleteResult.Acknowledged(0));

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new RemoveFriendCommandHandler(client.Object);
		var result = await handler.HandleAsync(new RemoveFriendCommand("auth0|me", "auth0|you"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsSuccess_WhenFriendshipRemoved()
	{
		var friendships = new Mock<IMongoCollection<FriendshipDocument>>();
		friendships.Setup(c => c.DeleteOneAsync(It.IsAny<FilterDefinition<FriendshipDocument>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new DeleteResult.Acknowledged(1));

		var database = new Mock<IMongoDatabase>();
		database.Setup(d => d.GetCollection<FriendshipDocument>("friendships", null)).Returns(friendships.Object);
		var client = new Mock<IMongoClient>();
		client.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(database.Object);

		var handler = new RemoveFriendCommandHandler(client.Object);
		var result = await handler.HandleAsync(new RemoveFriendCommand("auth0|me", "auth0|you"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
	}
}
