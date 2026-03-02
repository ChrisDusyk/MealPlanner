using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Recipes.Queries;

/// <summary>
/// Query to retrieve all recipes.
/// </summary>
public record GetAllRecipesQuery(string UserId) : IQuery<IReadOnlyList<Recipe>>;

/// <summary>
/// Handles retrieving all recipes from MongoDB.
/// </summary>
public class GetAllRecipesQueryHandler(IMongoClient mongoClient)
	: IQueryHandler<GetAllRecipesQuery, IReadOnlyList<Recipe>>
{
	public async Task<Result<IReadOnlyList<Recipe>>> HandleAsync(
		GetAllRecipesQuery query,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var collection = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<RecipeDocument>("recipes");

			var documents = await collection
				.Find(r => r.UserId == query.UserId)
				.ToListAsync(cancellationToken);

			var recipes = documents
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

	internal static Recipe MapToRecipe(RecipeDocument doc) =>
		new(
			Id: doc.Id!,
			UserId: doc.UserId,
			Name: doc.Name,
			Description: doc.Description,
			Servings: doc.Servings,
			SourceUrl: Option<string>.From(doc.SourceUrl),
			Ingredients: doc.Ingredients.Select(i => new Ingredient(i.Name, i.Quantity, i.Unit)).ToList(),
			CreatedAt: doc.CreatedAt,
			UpdatedAt: doc.UpdatedAt
		);
}
