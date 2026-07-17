using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Families;

/// <summary>
/// Shared logic for carrying a member's contributed recipes between families
/// when they join, leave, or are removed from a family group.
/// </summary>
internal static class FamilyRecipeMigration
{
	/// <summary>
	/// Copies the recipes a user contributed to <paramref name="sourceFamilyId"/>
	/// into <paramref name="targetFamilyId"/>. Recipes originating from the
	/// catalogue are skipped when the target family already has a copy (the
	/// unique (FamilyGroupId, CatalogueRecipeId) index is also enforced in
	/// application code because the InMemory test provider ignores indexes).
	/// Does NOT call SaveChanges — the caller owns the unit of work.
	/// </summary>
	internal static async Task CopyContributedRecipesAsync(
		MealPlannerDbContext db,
		Guid sourceFamilyId,
		Guid targetFamilyId,
		string contributorUserId,
		CancellationToken cancellationToken)
	{
		var contributed = await db.Recipes
			.AsNoTracking()
			.Where(r => r.FamilyGroupId == sourceFamilyId && r.ContributedByUserId == contributorUserId)
			.ToListAsync(cancellationToken);

		if (contributed.Count == 0)
			return;

		var catalogueIds = contributed
			.Where(r => r.CatalogueRecipeId is not null)
			.Select(r => r.CatalogueRecipeId!.Value)
			.ToList();

		var existingCatalogueIds = catalogueIds.Count == 0
			? []
			: await db.Recipes
				.AsNoTracking()
				.Where(r => r.FamilyGroupId == targetFamilyId
					&& r.CatalogueRecipeId != null
					&& catalogueIds.Contains(r.CatalogueRecipeId.Value))
				.Select(r => r.CatalogueRecipeId!.Value)
				.ToListAsync(cancellationToken);

		var alreadyInTarget = existingCatalogueIds.ToHashSet();
		var now = DateTime.UtcNow;

		foreach (var recipe in contributed)
		{
			if (recipe.CatalogueRecipeId is { } catalogueId && alreadyInTarget.Contains(catalogueId))
				continue;

			db.Recipes.Add(new RecipeEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = targetFamilyId,
				ContributedByUserId = contributorUserId,
				Name = recipe.Name,
				Description = recipe.Description,
				Servings = recipe.Servings,
				SourceUrl = recipe.SourceUrl,
				CatalogueRecipeId = recipe.CatalogueRecipeId,
				Ingredients = recipe.Ingredients
					.Select(i => new IngredientData
					{
						Name = i.Name,
						Quantity = i.Quantity,
						Unit = i.Unit,
						IsPantryStaple = i.IsPantryStaple
					})
					.ToList(),
				CreatedAt = now,
				UpdatedAt = now
			});
		}
	}

	/// <summary>
	/// Deletes a family group together with its content (meal plans, grocery
	/// lists, recipes, invitations, memberships). Postgres would cascade most
	/// of this, but the rows are removed explicitly so behaviour is identical
	/// under the InMemory test provider. Does NOT call SaveChanges.
	/// </summary>
	internal static async Task DeleteFamilyWithContentAsync(
		MealPlannerDbContext db,
		Guid familyGroupId,
		CancellationToken cancellationToken)
	{
		var mealPlans = await db.MealPlans
			.Where(p => p.FamilyGroupId == familyGroupId)
			.ToListAsync(cancellationToken);
		db.MealPlans.RemoveRange(mealPlans);

		var groceryLists = await db.GroceryLists
			.Where(g => g.FamilyGroupId == familyGroupId)
			.ToListAsync(cancellationToken);
		db.GroceryLists.RemoveRange(groceryLists);

		var recipes = await db.Recipes
			.Where(r => r.FamilyGroupId == familyGroupId)
			.ToListAsync(cancellationToken);
		db.Recipes.RemoveRange(recipes);

		var invitations = await db.FamilyInvitations
			.Where(i => i.FamilyGroupId == familyGroupId)
			.ToListAsync(cancellationToken);
		db.FamilyInvitations.RemoveRange(invitations);

		var members = await db.FamilyGroupMembers
			.Where(m => m.FamilyGroupId == familyGroupId)
			.ToListAsync(cancellationToken);
		db.FamilyGroupMembers.RemoveRange(members);

		var family = await db.FamilyGroups
			.FirstOrDefaultAsync(f => f.Id == familyGroupId, cancellationToken);
		if (family is not null)
			db.FamilyGroups.Remove(family);
	}
}
