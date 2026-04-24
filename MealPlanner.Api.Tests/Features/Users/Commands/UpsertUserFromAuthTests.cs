using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.Users.Commands;

public class UpsertUserFromAuthTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenAuth0IdMissing()
	{
		var handler = new UpsertUserFromAuthCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			new UpsertUserFromAuthCommand(" ", "Pat", Option<string>.Some("pat@example.com")),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_CreatesUser_WhenNoExistingUser()
	{
		var handler = new UpsertUserFromAuthCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			new UpsertUserFromAuthCommand("auth0|123", "Pat", Option<string>.Some("pat@example.com")),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal("auth0|123", result.Value!.AuthUserId);
		Assert.True(result.Value.Email.HasValue);
	}

	[Fact]
	public async Task HandleAsync_UpdatesExistingUser_WhenUserExists()
	{
		var now = DateTime.UtcNow.AddDays(-1);
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Users.Add(new UserEntity
			{
				Id = Guid.NewGuid(),
				AuthUserId = "auth0|123",
				Name = "Pat",
				Email = "old@example.com",
				CreatedAt = now,
				UpdatedAt = now
			});
		});

		var handler = new UpsertUserFromAuthCommandHandler(context);
		var result = await handler.HandleAsync(
			new UpsertUserFromAuthCommand("auth0|123", "Pat", Option<string>.Some("new@example.com")),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal("new@example.com", context.Users.Single().Email);
	}

	[Fact]
	public async Task HandleAsync_RelinksExistingUserByEmail_WhenAuthUserIdChanges()
	{
		// Simulates a user whose record was originally provisioned via Auth0
		// and is now signing in through Better Auth. The email should remain
		// the stable identifier and the row should be reused instead of a new
		// duplicate being created.
		var createdAt = DateTime.UtcNow.AddDays(-7);
		var existingId = Guid.NewGuid();
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Users.Add(new UserEntity
			{
				Id = existingId,
				AuthUserId = "auth0|legacy",
				Name = "Pat",
				Email = "pat@example.com",
				CreatedAt = createdAt,
				UpdatedAt = createdAt
			});
		});

		var handler = new UpsertUserFromAuthCommandHandler(context);
		var result = await handler.HandleAsync(
			new UpsertUserFromAuthCommand(
				"better-auth|new",
				"Pat",
				Option<string>.Some("pat@example.com")),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		var row = Assert.Single(context.Users);
		Assert.Equal(existingId, row.Id);
		Assert.Equal("better-auth|new", row.AuthUserId);
		Assert.Equal("pat@example.com", row.Email);
	}

	[Fact]
	public async Task HandleAsync_DoesNotRelink_WhenNoEmailProvided()
	{
		var createdAt = DateTime.UtcNow.AddDays(-7);
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.Users.Add(new UserEntity
			{
				Id = Guid.NewGuid(),
				AuthUserId = "auth0|legacy",
				Name = "Pat",
				Email = "pat@example.com",
				CreatedAt = createdAt,
				UpdatedAt = createdAt
			});
		});

		var handler = new UpsertUserFromAuthCommandHandler(context);
		var result = await handler.HandleAsync(
			new UpsertUserFromAuthCommand(
				"better-auth|new",
				"Pat",
				Option<string>.None()),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		// Without an email we can't safely re-link, so a brand new row should
		// be added alongside the legacy one.
		Assert.Equal(2, context.Users.Count());
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenDbUnavailable()
	{
		var context = TestDbContextFactory.CreateContext();
		context.Dispose();

		var handler = new UpsertUserFromAuthCommandHandler(context);
		var result = await handler.HandleAsync(
			new UpsertUserFromAuthCommand("auth0|123", "Pat", Option<string>.Some("pat@example.com")),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
