using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Queries;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Users.Queries;

public class FindUserByAuth0IdTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenAuth0IdMissing()
	{
		var handler = new FindUserByAuth0IdQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new FindUserByAuth0IdQuery(" "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsUser_WhenFound()
	{
		using var db = TestDbContextFactory.CreateContext();
		db.Users.Add(new UserEntity
		{
			Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
			Auth0UserId = "auth0|123",
			Name = "Pat",
			Email = "pat@example.com",
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
		});
		await db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var handler = new FindUserByAuth0IdQueryHandler(db);
		var result = await handler.HandleAsync(new FindUserByAuth0IdQuery("auth0|123"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal("auth0|123", result.Value?.Auth0UserId);
		Assert.True(result.Value?.Email.HasValue);
		Assert.Equal("pat@example.com", result.Value?.Email.Value);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenMissing()
	{
		using var db = TestDbContextFactory.CreateContext();
		var handler = new FindUserByAuth0IdQueryHandler(db);
		var result = await handler.HandleAsync(new FindUserByAuth0IdQuery("auth0|missing"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var db = TestDbContextFactory.CreateContext();
		db.Dispose();
		var handler = new FindUserByAuth0IdQueryHandler(db);
		var result = await handler.HandleAsync(new FindUserByAuth0IdQuery("auth0|123"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
