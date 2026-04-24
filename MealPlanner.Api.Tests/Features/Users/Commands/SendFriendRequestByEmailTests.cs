using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.Users.Commands;

public class SendFriendRequestByEmailTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenRequesterMissing()
	{
		var handler = new SendFriendRequestByEmailCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand(" ", "pal@example.com"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenRequesterUserMissing()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Users.Add(new UserEntity
			{
				Id = Guid.NewGuid(),
				AuthUserId = "auth0|you",
				Name = "You",
				Email = "you@example.com",
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
		});

		var handler = new SendFriendRequestByEmailCommandHandler(context);
		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand("auth0|missing", "you@example.com"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenRecipientEmailNotFound()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Users.Add(new UserEntity
			{
				Id = Guid.NewGuid(),
				AuthUserId = "auth0|me",
				Name = "Me",
				Email = "me@example.com",
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
		});

		var handler = new SendFriendRequestByEmailCommandHandler(context);
		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand("auth0|me", "missing@example.com"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenAlreadyFriends()
	{
		var now = DateTime.UtcNow;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Users.AddRange(
				new UserEntity
				{
					Id = Guid.NewGuid(),
					AuthUserId = "auth0|me",
					Name = "Me",
					Email = "me@example.com",
					CreatedAt = now,
					UpdatedAt = now
				},
				new UserEntity
				{
					Id = Guid.NewGuid(),
					AuthUserId = "auth0|you",
					Name = "You",
					Email = "you@example.com",
					CreatedAt = now,
					UpdatedAt = now
				});

			db.Friendships.Add(new FriendshipEntity
			{
				Id = Guid.NewGuid(),
				UserAId = "auth0|me",
				UserBId = "auth0|you",
				CreatedAt = now
			});
		});

		var handler = new SendFriendRequestByEmailCommandHandler(context);
		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand("auth0|me", "you@example.com"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_AutoAccepts_WhenReciprocalRequestExists()
	{
		var now = DateTime.UtcNow;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Users.AddRange(
				new UserEntity
				{
					Id = Guid.NewGuid(),
					AuthUserId = "auth0|me",
					Name = "Me",
					Email = "me@example.com",
					CreatedAt = now,
					UpdatedAt = now
				},
				new UserEntity
				{
					Id = Guid.NewGuid(),
					AuthUserId = "auth0|you",
					Name = "You",
					Email = "you@example.com",
					CreatedAt = now,
					UpdatedAt = now
				});

			db.FriendRequests.Add(new FriendRequestEntity
			{
				Id = Guid.NewGuid(),
				RequesterUserId = "auth0|you",
				RecipientUserId = "auth0|me",
				CreatedAt = now
			});
		});

		var handler = new SendFriendRequestByEmailCommandHandler(context);
		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand("auth0|me", "you@example.com"),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal(SendFriendRequestStatus.Accepted, result.Value?.Status);
		Assert.Empty(context.FriendRequests);
		Assert.Single(context.Friendships);
	}

	[Fact]
	public async Task HandleAsync_CreatesPendingRequest_WhenNoReciprocalExists()
	{
		var now = DateTime.UtcNow;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Users.AddRange(
				new UserEntity
				{
					Id = Guid.NewGuid(),
					AuthUserId = "auth0|me",
					Name = "Me",
					Email = "me@example.com",
					CreatedAt = now,
					UpdatedAt = now
				},
				new UserEntity
				{
					Id = Guid.NewGuid(),
					AuthUserId = "auth0|you",
					Name = "You",
					Email = "you@example.com",
					CreatedAt = now,
					UpdatedAt = now
				});
		});

		var handler = new SendFriendRequestByEmailCommandHandler(context);
		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand("auth0|me", "you@example.com"),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal(SendFriendRequestStatus.Pending, result.Value?.Status);
		Assert.Single(context.FriendRequests);
		Assert.Equal("auth0|me", context.FriendRequests.Single().RequesterUserId);
		Assert.Equal("auth0|you", context.FriendRequests.Single().RecipientUserId);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenDuplicateOutgoingRequestExists()
	{
		var now = DateTime.UtcNow;
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Users.AddRange(
				new UserEntity
				{
					Id = Guid.NewGuid(),
					AuthUserId = "auth0|me",
					Name = "Me",
					Email = "me@example.com",
					CreatedAt = now,
					UpdatedAt = now
				},
				new UserEntity
				{
					Id = Guid.NewGuid(),
					AuthUserId = "auth0|you",
					Name = "You",
					Email = "you@example.com",
					CreatedAt = now,
					UpdatedAt = now
				});

			db.FriendRequests.Add(new FriendRequestEntity
			{
				Id = Guid.NewGuid(),
				RequesterUserId = "auth0|me",
				RecipientUserId = "auth0|you",
				CreatedAt = now
			});
		});

		var handler = new SendFriendRequestByEmailCommandHandler(context);
		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand("auth0|me", "you@example.com"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}
}
