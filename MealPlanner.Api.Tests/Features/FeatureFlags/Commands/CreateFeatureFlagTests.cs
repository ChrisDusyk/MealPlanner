using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.FeatureFlags;
using MealPlanner.Api.Features.FeatureFlags.Commands;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Tests.Features.FeatureFlags.Commands;

public class CreateFeatureFlagTests
{
	private const string BooleanDefinition =
		"{\"variants\":{\"on\":true,\"off\":false},\"defaultVariant\":\"on\"}";

	private static CreateFeatureFlagCommand Command(
		string key = "new-flag",
		bool enabled = true,
		string valueType = FeatureFlagValueTypes.Boolean,
		string? disabledVariant = "off",
		string definitionJson = BooleanDefinition,
		string? description = "A new flag.") =>
		new(key, enabled, valueType, disabledVariant, definitionJson, description);

	[Fact]
	public async Task HandleAsync_CreatesTheFlag_AndPersistsEveryField()
	{
		var databaseName = $"mealplanner-tests-{Guid.NewGuid():N}";
		var handler = new CreateFeatureFlagCommandHandler(
			TestDbContextFactory.CreateContext(databaseName));

		var result = await handler.HandleAsync(Command(), TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);

		using var verification = TestDbContextFactory.CreateContext(databaseName);
		var stored = await verification.FeatureFlags.SingleAsync(
			f => f.Key == "new-flag", TestContext.Current.CancellationToken);

		Assert.True(stored.Enabled);
		Assert.Equal(FeatureFlagValueTypes.Boolean, stored.ValueType);
		Assert.Equal("off", stored.DisabledVariant);
		Assert.Equal(BooleanDefinition, stored.DefinitionJson);
		Assert.Equal("A new flag.", stored.Description);
	}

	[Fact]
	public async Task HandleAsync_StoresBlankOptionalFields_AsNull()
	{
		var databaseName = $"mealplanner-tests-{Guid.NewGuid():N}";
		var handler = new CreateFeatureFlagCommandHandler(
			TestDbContextFactory.CreateContext(databaseName));

		var result = await handler.HandleAsync(
			Command(disabledVariant: "   ", description: "  "),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);

		using var verification = TestDbContextFactory.CreateContext(databaseName);
		var stored = await verification.FeatureFlags.SingleAsync(
			f => f.Key == "new-flag", TestContext.Current.CancellationToken);

		Assert.Null(stored.DisabledVariant);
		Assert.Null(stored.Description);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenKeyIsInvalid()
	{
		var handler = new CreateFeatureFlagCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			Command(key: "Not A Key"), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenDefinitionIsInvalid()
	{
		var handler = new CreateFeatureFlagCommandHandler(TestDbContextFactory.CreateContext());

		var result = await handler.HandleAsync(
			Command(definitionJson: "not-json", disabledVariant: null),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsConflict_WhenTheKeyAlreadyExists()
	{
		var context = TestDbContextFactory.CreateContext(db =>
		{
			db.FeatureFlags.Add(new FeatureFlagEntity
			{
				Key = "new-flag",
				Enabled = false,
				ValueType = FeatureFlagValueTypes.Boolean,
				DefinitionJson = BooleanDefinition,
				UpdatedAt = DateTime.UtcNow
			});
		});

		var handler = new CreateFeatureFlagCommandHandler(context);

		var result = await handler.HandleAsync(Command(), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.Conflict, result.Error?.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsDatabaseError_WhenTheContextIsDisposed()
	{
		var context = TestDbContextFactory.CreateContext();
		await context.DisposeAsync();

		var handler = new CreateFeatureFlagCommandHandler(context);

		var result = await handler.HandleAsync(Command(), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.Equal(ErrorCodes.DatabaseError, result.Error?.Code);
	}
}
