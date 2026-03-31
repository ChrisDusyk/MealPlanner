using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Recipes.Queries;

/// <summary>
/// Query to retrieve all recipes.
/// </summary>
public record GetAllRecipesQuery(string UserId) : IQuery<IReadOnlyList<Recipe>>;

/// <summary>
/// Handles retrieving all recipes.
/// </summary>
public class GetAllRecipesQueryHandler(MealPlannerDbContext db)
	: IQueryHandler<GetAllRecipesQuery, IReadOnlyList<Recipe>>
{
	public async Task<Result<IReadOnlyList<Recipe>>> HandleAsync(
		GetAllRecipesQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var entities = await db.Recipes
				.Where(r => r.UserId == query.UserId)
				.ToListAsync(cancellationToken);

			var recipes = entities
				.Select(MapToRecipe)
				.ToList() as IReadOnlyList<Recipe>;

			return Result<IReadOnlyList<Recipe>>.Success(recipes);
		}
		catch (Exception ex)
		{
			return Result<IReadOnlyList<Recipe>>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve recipes.", ex));
		}
	}

	internal static Recipe MapToRecipe(RecipeEntity entity) =>
		new(
			Id: entity.Id.ToString(),
			UserId: entity.UserId,
			Name: entity.Name,
			Description: entity.Description,
			Servings: entity.Servings,
			SourceUrl: Option<string>.From(entity.SourceUrl),
			Ingredients: entity.Ingredients.Select(i => new Ingredient(i.Name, i.Quantity, i.Unit, i.IsPantryStaple)).ToList(),
			CreatedAt: entity.CreatedAt,
			UpdatedAt: entity.UpdatedAt
		);
}
