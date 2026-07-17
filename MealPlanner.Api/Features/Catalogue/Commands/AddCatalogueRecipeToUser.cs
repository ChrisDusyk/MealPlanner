using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.Recipes.Commands;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Catalogue.Commands;

/// <summary>
/// Adds a catalogue recipe to the current user's recipe collection by copying
/// the data into a new <see cref="RecipeEntity"/>. Returns a domain
/// <see cref="Recipe"/> (mirrors <see cref="CreateRecipeCommand"/>).
/// Blocks duplicates via the unique partial index on
/// <c>(FamilyGroupId, CatalogueRecipeId)</c>.
/// </summary>
public record AddCatalogueRecipeToUserCommand(Guid CatalogueRecipeId, Guid FamilyGroupId, string ContributedByUserId)
	: ICommand<Recipe>;

public class AddCatalogueRecipeToUserCommandHandler(MealPlannerDbContext db)
	: ICommandHandler<AddCatalogueRecipeToUserCommand, Recipe>
{
	public async Task<Result<Recipe>> HandleAsync(
		AddCatalogueRecipeToUserCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.ContributedByUserId))
		{
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.ValidationFailed, "User identifier is required."));
		}

		try
		{
			var catalogueEntity = await db.CatalogueRecipes
				.FirstOrDefaultAsync(r => r.Id == command.CatalogueRecipeId, cancellationToken);

			if (catalogueEntity is null || !catalogueEntity.IsPublished)
			{
				return Result<Recipe>.Failure(
					new Error(ErrorCodes.NotFound, "Catalogue recipe was not found."));
			}

			var alreadyAdded = await db.Recipes
				.AsNoTracking()
				.AnyAsync(
					r => r.FamilyGroupId == command.FamilyGroupId && r.CatalogueRecipeId == catalogueEntity.Id,
					cancellationToken);

			if (alreadyAdded)
			{
				return Result<Recipe>.Failure(
					new Error(ErrorCodes.ValidationFailed, "Already in your recipes."));
			}

			var now = DateTime.UtcNow;
			var recipe = new RecipeEntity
			{
				Id = Guid.NewGuid(),
				FamilyGroupId = command.FamilyGroupId,
				ContributedByUserId = command.ContributedByUserId,
				Name = catalogueEntity.Name,
				Description = catalogueEntity.Description,
				Servings = catalogueEntity.Servings,
				SourceUrl = catalogueEntity.SourceUrl,
				CatalogueRecipeId = catalogueEntity.Id,
				Ingredients = catalogueEntity.Ingredients
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

			db.Recipes.Add(recipe);
			catalogueEntity.AddCount += 1;

			await db.SaveChangesAsync(cancellationToken);

			return Result<Recipe>.Success(MapToRecipe(recipe));
		}
		catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex))
		{
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.ValidationFailed, "Already in your recipes.", ex));
		}
		catch (Exception ex)
		{
			return Result<Recipe>.Failure(
				new Error(ErrorCodes.DatabaseError, "Failed to add catalogue recipe.", ex));
		}
	}

	private static bool IsUniqueIndexViolation(DbUpdateException ex)
	{
		// Npgsql throws a PostgresException with SqlState 23505 for unique
		// violations. Match by name to avoid taking a Npgsql package reference
		// in tests.
		var inner = ex.InnerException;
		return inner is not null
		       && inner.GetType().Name == "PostgresException"
		       && (inner.GetType().GetProperty("SqlState")?.GetValue(inner) as string) == "23505";
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
			Ingredients: entity.Ingredients
				.Select(i => new Ingredient(i.Name, i.Quantity, i.Unit, i.IsPantryStaple))
				.ToList(),
			CreatedAt: entity.CreatedAt,
			UpdatedAt: entity.UpdatedAt
		);
}
