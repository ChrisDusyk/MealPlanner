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
		Assert.Equal("auth0|123", result.Value!.Auth0UserId);
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
				Auth0UserId = "auth0|123",
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