using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;

namespace MealPlanner.Api.Tests.TestUtilities;

/// <summary>
/// Seed helpers for family-group test fixtures.
/// </summary>
internal static class FamilyTestData
{
	internal static FamilyGroupEntity SeedFamily(
		MealPlannerDbContext db,
		string familyKey,
		string ownerUserId,
		params string[] otherMemberUserIds)
	{
		var family = new FamilyGroupEntity
		{
			Id = TestIds.Family(familyKey),
			Name = familyKey + " family",
			OwnerUserId = ownerUserId,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		db.FamilyGroups.Add(family);

		foreach (var userId in otherMemberUserIds.Prepend(ownerUserId))
		{
			db.FamilyGroupMembers.Add(new FamilyGroupMemberEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = family.Id,
				UserId = userId,
				JoinedAt = DateTime.UtcNow
			});
		}

		return family;
	}

	internal static UserEntity SeedUser(
		MealPlannerDbContext db,
		string authUserId,
		string name,
		string? email = null)
	{
		var user = new UserEntity
		{
			Id = Guid.NewGuid(),
			AuthUserId = authUserId,
			Name = name,
			Email = email,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		db.Users.Add(user);
		return user;
	}

	internal static RecipeEntity SeedRecipe(
		MealPlannerDbContext db,
		Guid familyGroupId,
		string contributedByUserId,
		string name,
		Guid? catalogueRecipeId = null)
	{
		var recipe = new RecipeEntity
		{
			Id = Guid.NewGuid(),
			FamilyGroupId = familyGroupId,
			ContributedByUserId = contributedByUserId,
			Name = name,
			Description = string.Empty,
			Servings = 2,
			CatalogueRecipeId = catalogueRecipeId,
			Ingredients = [new IngredientData { Name = "Salt", Quantity = 1, Unit = "tsp" }],
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		db.Recipes.Add(recipe);
		return recipe;
	}
}
