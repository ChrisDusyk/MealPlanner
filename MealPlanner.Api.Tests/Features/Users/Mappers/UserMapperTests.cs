using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Users.Mappers;

namespace MealPlanner.Api.Tests.Features.Users.Mappers;

/// <summary>
/// Direct coverage for the centralised <see cref="UserMapper"/> which is used
/// by every user-feature handler, per the project testing guidelines that
/// internal mapper methods must have dedicated tests.
/// </summary>
public class UserMapperTests
{
	private static UserEntity CreateEntity(Action<UserEntity>? customize = null)
	{
		var entity = new UserEntity
		{
			Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
			AuthUserId = "better-auth|123",
			Name = "Pat",
			Email = "pat@example.com",
			CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
		};
		customize?.Invoke(entity);
		return entity;
	}

	[Fact]
	public void ToDomain_MapsAllFields_WhenFullyPopulated()
	{
		var completed = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);
		var user = UserMapper.ToDomain(CreateEntity(entity =>
		{
			entity.DisplayName = "Pat H.";
			entity.Timezone = "America/Toronto";
			entity.OnboardingCompletedAt = completed;
		}));

		Assert.Equal("11111111-1111-1111-1111-111111111111", user.Id);
		Assert.Equal("better-auth|123", user.AuthUserId);
		Assert.Equal("Pat", user.Name);
		Assert.True(user.Email.HasValue);
		Assert.Equal("pat@example.com", user.Email.Value);
		Assert.True(user.DisplayName.HasValue);
		Assert.Equal("Pat H.", user.DisplayName.Value);
		Assert.True(user.Timezone.HasValue);
		Assert.Equal("America/Toronto", user.Timezone.Value);
		Assert.True(user.OnboardingCompletedAt.HasValue);
		Assert.Equal(completed, user.OnboardingCompletedAt.Value);
	}

	[Fact]
	public void ToDomain_MapsOptionalFieldsToNone_WhenEntityHasNulls()
	{
		var user = UserMapper.ToDomain(CreateEntity(entity =>
		{
			entity.Email = null;
			entity.DisplayName = null;
			entity.Timezone = null;
			entity.OnboardingCompletedAt = null;
		}));

		Assert.False(user.Email.HasValue);
		Assert.False(user.DisplayName.HasValue);
		Assert.False(user.Timezone.HasValue);
		Assert.False(user.OnboardingCompletedAt.HasValue);
	}

	[Fact]
	public void ToDomain_PreservesCreatedAndUpdatedTimestamps()
	{
		var entity = CreateEntity();

		var user = UserMapper.ToDomain(entity);

		Assert.Equal(entity.CreatedAt, user.CreatedAt);
		Assert.Equal(entity.UpdatedAt, user.UpdatedAt);
	}
}
