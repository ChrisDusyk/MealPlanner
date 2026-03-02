using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Recipes.Commands;

/// <summary>
/// Command to create a new recipe.
/// </summary>
public record CreateRecipeCommand(
	string UserId,
	string Name,
	string Description,
	int Servings,
	Option<string> SourceUrl,
	List<Ingredient> Ingredients
) : ICommand<Recipe>;

/// <summary>
/// Handles creating a new recipe in MongoDB.
/// </summary>
public class CreateRecipeCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<CreateRecipeCommand, Recipe>
{
	public async Task<Result<Recipe>> HandleAsync(
		CreateRecipeCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.Name))
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipe name is required."));

		if (command.Servings < 1)
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipe servings must be at least 1."));

		try
		{
			var now = DateTime.UtcNow;
			var document = new RecipeDocument
			{
				UserId = command.UserId,
				Name = command.Name,
				Description = command.Description,
				Servings = command.Servings,
				SourceUrl = command.SourceUrl.GetValueOrNull(),
				Ingredients = command.Ingredients
					.Select(i => new IngredientDocument
					{
						Name = i.Name,
						Quantity = i.Quantity,
						Unit = i.Unit,
						IsPantryStaple = i.IsPantryStaple
					})
					.ToList(),
				CreatedAt = now,
				UpdatedAt = now
			};

			var collection = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<RecipeDocument>("recipes");

			await collection.InsertOneAsync(document, cancellationToken: cancellationToken);

			return Result<Recipe>.Success(MapToRecipe(document));
		}
		catch (Exception ex)
		{
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to create recipe.", ex));
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
