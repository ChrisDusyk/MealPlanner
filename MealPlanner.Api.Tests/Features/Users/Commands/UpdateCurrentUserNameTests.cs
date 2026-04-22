using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Users.Commands;

public class UpdateCurrentUserNameTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenAuth0IdMissing()
	{
		var handler = new UpdateCurrentUserNameCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new UpdateCurrentUserNameCommand(" ", "Pat"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenNameMissing()
	{
		var handler = new UpdateCurrentUserNameCommandHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new UpdateCurrentUserNameCommand("auth0|123", " "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsUser_WhenUpdateSucceeds()
	{
		using var db = TestDbContextFactory.CreateContext();
		db.Users.Add(new UserEntity
		{
			Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
			Auth0UserId = "auth0|123",
			Name = "Pat",
			Email = "pat@example.com",
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
		});
		await db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var handler = new UpdateCurrentUserNameCommandHandler(db);
		var result = await handler.HandleAsync(new UpdateCurrentUserNameCommand("auth0|123", "Updated Name"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal("Updated Name", result.Value?.Name);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenUserMissing()
	{
		using var db = TestDbContextFactory.CreateContext();
		var handler = new UpdateCurrentUserNameCommandHandler(db);
		var result = await handler.HandleAsync(new UpdateCurrentUserNameCommand("auth0|123", "Updated Name"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var db = TestDbContextFactory.CreateContext();
		db.Dispose();
		var handler = new UpdateCurrentUserNameCommandHandler(db);
		var result = await handler.HandleAsync(new UpdateCurrentUserNameCommand("auth0|123", "Updated Name"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
