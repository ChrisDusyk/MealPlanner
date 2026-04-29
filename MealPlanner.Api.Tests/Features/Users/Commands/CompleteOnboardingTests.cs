using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;

namespace MealPlanner.Api.Tests.Features.Users.Commands;

public class CompleteOnboardingTests
{
	private const string SeedAuthUserId = "better-auth|abc";

	private static void SeedExistingUser(MealPlannerDbContext db, Action<UserEntity>? customize = null)
	{
		var entity = new UserEntity
		{
			Id = Guid.NewGuid(),
			AuthUserId = SeedAuthUserId,
			Name = "Pat",
			Email = "pat@example.com",
			CreatedAt = DateTime.UtcNow.AddDays(-1),
			UpdatedAt = DateTime.UtcNow.AddDays(-1)
		};
		customize?.Invoke(entity);
		db.Users.Add(entity);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenAuthUserIdMissing()
	{
		var handler = new CompleteOnboardingCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			new CompleteOnboardingCommand(" ", Option<string>.Some("Pat"), Option<string>.None()),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsNotFound_WhenUserDoesNotExist()
	{
		var handler = new CompleteOnboardingCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			new CompleteOnboardingCommand(SeedAuthUserId, Option<string>.Some("Pat"), Option<string>.None()),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.NotFound, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_PersistsProfile_AndMarksOnboardingComplete()
	{
		var context = TestDbContextFactory.CreateContext(seed: db => SeedExistingUser(db));
		var handler = new CompleteOnboardingCommandHandler(context);

		var before = DateTime.UtcNow;
		var result = await handler.HandleAsync(
			new CompleteOnboardingCommand(
				SeedAuthUserId,
				Option<string>.Some("Patricia H."),
				Option<string>.Some("America/Toronto")),
			TestContext.Current.CancellationToken);
		var after = DateTime.UtcNow;

		Assert.True(result.IsSuccess);
		var row = Assert.Single(context.Users);
		Assert.Equal("Patricia H.", row.DisplayName);
		Assert.Equal("America/Toronto", row.Timezone);
		Assert.NotNull(row.OnboardingCompletedAt);
		Assert.InRange(row.OnboardingCompletedAt!.Value, before.AddSeconds(-1), after.AddSeconds(1));
	}

	[Fact]
	public async Task HandleAsync_DoesNotOverwriteOnboardingTimestamp_WhenAlreadySet()
	{
		var originalCompletedAt = DateTime.UtcNow.AddDays(-5);
		var context = TestDbContextFactory.CreateContext(seed: db => SeedExistingUser(db, entity =>
		{
			entity.DisplayName = "Old Name";
			entity.Timezone = "UTC";
			entity.OnboardingCompletedAt = originalCompletedAt;
		}));
		var handler = new CompleteOnboardingCommandHandler(context);

		var result = await handler.HandleAsync(
			new CompleteOnboardingCommand(
				SeedAuthUserId,
				Option<string>.Some("New Name"),
				Option<string>.Some("America/Toronto")),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		var row = Assert.Single(context.Users);
		Assert.Equal("New Name", row.DisplayName);
		Assert.Equal("America/Toronto", row.Timezone);
		Assert.Equal(originalCompletedAt, row.OnboardingCompletedAt);
	}

	[Fact]
	public async Task HandleAsync_TrimsWhitespace_ButIgnoresEmptyStrings()
	{
		var context = TestDbContextFactory.CreateContext(seed: db => SeedExistingUser(db, entity =>
		{
			entity.DisplayName = "Original";
			entity.Timezone = "UTC";
		}));
		var handler = new CompleteOnboardingCommandHandler(context);

		var result = await handler.HandleAsync(
			new CompleteOnboardingCommand(
				SeedAuthUserId,
				Option<string>.Some("   "),
				Option<string>.Some("  America/Toronto  ")),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		var row = Assert.Single(context.Users);
		// Empty (whitespace-only) display names should leave the existing value
		// untouched so users don't accidentally clear previously-saved data.
		Assert.Equal("Original", row.DisplayName);
		Assert.Equal("America/Toronto", row.Timezone);
	}
}
