using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Recipes.Queries;

/// <summary>
/// Query to retrieve a single recipe by its ID.
/// </summary>
public record GetRecipeByIdQuery(string Id, string UserId) : IQuery<Recipe>;

/// <summary>
/// Handles retrieving a single recipe from MongoDB by ID.
/// </summary>
public class GetRecipeByIdQueryHandler(IMongoClient mongoClient)
	: IQueryHandler<GetRecipeByIdQuery, Recipe>
{
	public async Task<Result<Recipe>> HandleAsync(
		GetRecipeByIdQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var collection = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<RecipeDocument>("recipes");

			var document = await collection
				.Find(r => r.Id == query.Id)
				.FirstOrDefaultAsync(cancellationToken);

			if (document is null)
				return Result<Recipe>.Failure(
					new Error(ErrorCodes.NotFound, $"Recipe with ID '{query.Id}' was not found."));

			if (document.UserId != query.UserId)
				return Result<Recipe>.Failure(
					new Error(ErrorCodes.Unauthorized, "You do not have permission to view this recipe."));

			return Result<Recipe>.Success(MapToRecipe(document));
		}
		catch (Exception ex)
		{
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to retrieve recipe.", ex));
		}
	}

	internal static Recipe MapToRecipe(RecipeDocument doc) =>
		new(
			Id: doc.Id!,
			UserId: doc.UserId,
			Name: doc.Name,
			Description: doc.Description,
			Servings: doc.Servings,
			SourceUrl: Option<string>.From(doc.SourceUrl),
			Ingredients: doc.Ingredients.Select(i => new Ingredient(i.Name, i.Quantity, i.Unit, i.IsPantryStaple)).ToList(),
			CreatedAt: doc.CreatedAt,
			UpdatedAt: doc.UpdatedAt
		);
}
