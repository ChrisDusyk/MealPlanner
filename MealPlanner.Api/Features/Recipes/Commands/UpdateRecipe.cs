using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Recipes.Commands;

/// <summary>
/// Command to update an existing recipe.
/// </summary>
public record UpdateRecipeCommand(
	string Id,
	string UserId,
	string Name,
	string Description,
	int Servings,
	Option<string> SourceUrl,
	List<Ingredient> Ingredients
) : ICommand<Recipe>;

/// <summary>
/// Handles updating an existing recipe.
/// </summary>
public class UpdateRecipeCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<UpdateRecipeCommand, Recipe>
{
	public async Task<Result<Recipe>> HandleAsync(
		UpdateRecipeCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.Name))
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipe name is required."));

		if (command.Servings < 1)
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipe servings must be at least 1."));

		if (!Guid.TryParse(command.Id, out var recipeGuid))
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipe ID is invalid."));

		try
		{
			var entity = await db.Recipes
				.FirstOrDefaultAsync(r => r.Id == recipeGuid, cancellationToken);

			if (entity is null)
				return Result<Recipe>.Failure(
					new Error(ErrorCodes.NotFound, $"Recipe with ID '{command.Id}' was not found."));

			if (entity.UserId != command.UserId)
				return Result<Recipe>.Failure(
					new Error(ErrorCodes.Unauthorized, "You do not have permission to update this recipe."));

			entity.Name = command.Name;
			entity.Description = command.Description;
			entity.Servings = command.Servings;
			entity.SourceUrl = command.SourceUrl.GetValueOrNull();
			entity.Ingredients = command.Ingredients
				.Select(i => new IngredientData
				{
					Name = i.Name,
					Quantity = i.Quantity,
					Unit = i.Unit,
					IsPantryStaple = i.IsPantryStaple
				})
				.ToList();
			entity.UpdatedAt = DateTime.UtcNow;

			await db.SaveChangesAsync(cancellationToken);

			return Result<Recipe>.Success(MapToRecipe(entity));
		}
		catch (Exception ex)
		{
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to update recipe.", ex));
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
