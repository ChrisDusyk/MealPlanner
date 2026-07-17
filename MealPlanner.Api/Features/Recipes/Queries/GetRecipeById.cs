using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Recipes.Queries;

/// <summary>
/// Query to retrieve a single recipe by its ID.
/// </summary>
public record GetRecipeByIdQuery(string Id, Guid FamilyGroupId) : IQuery<Recipe>;

/// <summary>
/// Handles retrieving a single recipe by ID.
/// </summary>
public class GetRecipeByIdQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetRecipeByIdQuery, Recipe>
{
	public async Task<Result<Recipe>> HandleAsync(
		GetRecipeByIdQuery query,
		CancellationToken cancellationToken = default)
	{
		if (!Guid.TryParse(query.Id, out var recipeGuid))
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipe ID is invalid."));

		try
		{
			var entity = await db.Recipes
				.FirstOrDefaultAsync(r => r.Id == recipeGuid, cancellationToken);

			if (entity is null)
				return Result<Recipe>.Failure(
					new Error(ErrorCodes.NotFound, $"Recipe with ID '{query.Id}' was not found."));

			if (entity.FamilyGroupId != query.FamilyGroupId)
				return Result<Recipe>.Failure(
					new Error(ErrorCodes.Unauthorized, "You do not have permission to view this recipe."));

			return Result<Recipe>.Success(MapToRecipe(entity));
		}
		catch (Exception ex)
		{
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve recipe.", ex));
		}
	}

	internal static Recipe MapToRecipe(RecipeEntity entity) =>
		new(
			Id: entity.Id.ToString(),
			FamilyGroupId: entity.FamilyGroupId.ToString(),
			ContributedByUserId: entity.ContributedByUserId,
			Name: entity.Name,
			Description: entity.Description,
			Servings: entity.Servings,
			SourceUrl: Option<string>.From(entity.SourceUrl),
			Ingredients: entity.Ingredients.Select(i => new Ingredient(i.Name, i.Quantity, i.Unit, i.IsPantryStaple)).ToList(),
			CreatedAt: entity.CreatedAt,
			UpdatedAt: entity.UpdatedAt
		);
}
