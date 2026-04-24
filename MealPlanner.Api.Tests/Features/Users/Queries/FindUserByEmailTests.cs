using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Queries;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Tests.Features.Users.Queries;

public class FindUserByEmailTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenEmailMissing()
	{
		var handler = new FindUserByEmailQueryHandler(TestDbContextFactory.CreateContext());
		var result = await handler.HandleAsync(new FindUserByEmailQuery(" "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsUser_WhenFound()
	{
		using var db = TestDbContextFactory.CreateContext();
		db.Users.Add(new UserEntity
		{
			Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
			AuthUserId = "auth0|123",
			Name = "Pat",
			Email = "pat@example.com",
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
		});
		await db.SaveChangesAsync(TestContext.Current.CancellationToken);

		var handler = new FindUserByEmailQueryHandler(db);
		var result = await handler.HandleAsync(new FindUserByEmailQuery("pat@example.com"), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.Equal("auth0|123", result.Value?.AuthUserId);
		Assert.True(result.Value?.Email.HasValue);
		Assert.Equal("pat@example.com", result.Value?.Email.Value);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenMissing()
	{
		using var db = TestDbContextFactory.CreateContext();
		var handler = new FindUserByEmailQueryHandler(db);
		var result = await handler.HandleAsync(new FindUserByEmailQuery("missing@example.com"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenContextDisposed()
	{
		var db = TestDbContextFactory.CreateContext();
		db.Dispose();
		var handler = new FindUserByEmailQueryHandler(db);
		var result = await handler.HandleAsync(new FindUserByEmailQuery("pat@example.com"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
