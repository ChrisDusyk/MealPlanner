using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Recipes.Commands;

/// <summary>
/// Command to create a new recipe.
/// </summary>
public record CreateRecipeCommand(
	Guid FamilyGroupId,
	string ContributedByUserId,
	string Name,
	string Description,
	int Servings,
	Option<string> SourceUrl,
	List<Ingredient> Ingredients
) : ICommand<Recipe>;

/// <summary>
/// Handles creating a new recipe.
/// </summary>
public class CreateRecipeCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<CreateRecipeCommand, Recipe>
{
	public async Task<Result<Recipe>> HandleAsync(
		CreateRecipeCommand command,
		CancellationToken cancellationToken = default)
	{
		if (command.FamilyGroupId == Guid.Empty)
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Family group ID is required."));

		if (string.IsNullOrWhiteSpace(command.ContributedByUserId))
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Contributor user ID is required."));

		if (string.IsNullOrWhiteSpace(command.Name))
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipe name is required."));

		if (command.Servings < 1)
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Recipe servings must be at least 1."));

		try
		{
			var now = DateTime.UtcNow;
			var entity = new RecipeEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = command.FamilyGroupId,
				ContributedByUserId = command.ContributedByUserId,
				Name = command.Name,
				Description = command.Description,
				Servings = command.Servings,
				SourceUrl = command.SourceUrl.GetValueOrNull(),
				Ingredients = command.Ingredients
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
			};

			db.Recipes.Add(entity);
			await db.SaveChangesAsync(cancellationToken);

			return Result<Recipe>.Success(MapToRecipe(entity));
		}
		catch (Exception ex)
		{
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to create recipe.", ex));
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
