using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.Recipes.Commands;

/// <summary>
/// Command to update an existing recipe.
/// </summary>
public record UpdateRecipeCommand(
	string Id,
	string UserId,
	string Name,
	string Description,
	Option<string> SourceUrl,
	List<Ingredient> Ingredients
) : ICommand<Recipe>;

/// <summary>
/// Handles updating an existing recipe in MongoDB.
/// </summary>
public class UpdateRecipeCommandHandler(IMongoClient mongoClient)
	: ICommandHandler<UpdateRecipeCommand, Recipe>
{
	public async Task<Result<Recipe>> HandleAsync(
		UpdateRecipeCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.Name))
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipe name is required."));

		try
		{
			var collection = mongoClient
				.GetDatabase("mealplannerDb")
				.GetCollection<RecipeDocument>("recipes");

			var existing = await collection
				.Find(r => r.Id == command.Id)
				.FirstOrDefaultAsync(cancellationToken);

			if (existing is null)
				return Result<Recipe>.Failure(
					new Error(ErrorCodes.NotFound, $"Recipe with ID '{command.Id}' was not found."));

			if (existing.UserId != command.UserId)
				return Result<Recipe>.Failure(
					new Error(ErrorCodes.Unauthorized, "You do not have permission to update this recipe."));

			var updatedDocument = new RecipeDocument
			{
				Id = existing.Id,
				UserId = existing.UserId,
				Name = command.Name,
				Description = command.Description,
				SourceUrl = command.SourceUrl.GetValueOrNull(),
				Ingredients = command.Ingredients
					.Select(i => new IngredientDocument
					{
						Name = i.Name,
						Quantity = i.Quantity,
						Unit = i.Unit
					})
					.ToList(),
				CreatedAt = existing.CreatedAt,
				UpdatedAt = DateTime.UtcNow
			};

			await collection.ReplaceOneAsync(
				r => r.Id == command.Id,
				updatedDocument,
				cancellationToken: cancellationToken);

			return Result<Recipe>.Success(MapToRecipe(updatedDocument));
		}
		catch (Exception ex)
		{
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to update recipe.", ex));
		}
	}

	internal static Recipe MapToRecipe(RecipeDocument doc) =>
		new(
			Id: doc.Id!,
			UserId: doc.UserId,
			Name: doc.Name,
			Description: doc.Description,
			SourceUrl: Option<string>.From(doc.SourceUrl),
			Ingredients: doc.Ingredients.Select(i => new Ingredient(i.Name, i.Quantity, i.Unit)).ToList(),
			CreatedAt: doc.CreatedAt,
			UpdatedAt: doc.UpdatedAt
		);
}
