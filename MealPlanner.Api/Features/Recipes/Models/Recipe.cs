using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Recipes.Models;

/// <summary>
/// Domain record representing a food recipe owned by a family group.
/// ContributedByUserId tracks which member created the recipe (or brought it
/// into the family), so it can travel with them if they leave.
/// </summary>
public record Recipe(
	string Id,
	string FamilyGroupId,
	string ContributedByUserId,
	string Name,
	string Description,
	int Servings,
	Option<string> SourceUrl,
	List<Ingredient> Ingredients,
	DateTime CreatedAt,
	DateTime UpdatedAt
);
