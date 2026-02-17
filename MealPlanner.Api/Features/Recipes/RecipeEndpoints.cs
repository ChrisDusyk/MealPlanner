using MealPlanner.Api.Features.Recipes.Commands;
using MealPlanner.Api.Features.Recipes.Dtos;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Features.Recipes.Queries;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Recipes;

/// <summary>
/// Maps minimal API endpoints for recipe management.
/// </summary>
public static class RecipeEndpoints
{
	public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/recipes")
			.WithTags("Recipes");

		group.MapGet("/", GetAllRecipes);
		group.MapGet("/{id}", GetRecipeById);
		group.MapPost("/", CreateRecipe);
		group.MapPut("/{id}", UpdateRecipe);

		return app;
	}

	private static async Task<IResult> GetAllRecipes(
		IQueryHandler<GetAllRecipesQuery, IReadOnlyList<Recipe>> handler,
		CancellationToken cancellationToken)
	{
		var result = await handler.HandleAsync(new GetAllRecipesQuery(), cancellationToken);
		return result.Match(
			onSuccess: recipes => Results.Ok(recipes.Select(RecipeResponse.FromDomain).ToList()),
			onFailure: error => Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> GetRecipeById(
		string id,
		IQueryHandler<GetRecipeByIdQuery, Recipe> handler,
		CancellationToken cancellationToken)
	{
		var result = await handler.HandleAsync(new GetRecipeByIdQuery(id), cancellationToken);
		return result.Match(
			onSuccess: recipe => Results.Ok(RecipeResponse.FromDomain(recipe)),
			onFailure: error => error.Code == ErrorCodes.NotFound
				? Results.NotFound(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> CreateRecipe(
		CreateRecipeRequest request,
		ICommandHandler<CreateRecipeCommand, Recipe> handler,
		CancellationToken cancellationToken)
	{
		var command = new CreateRecipeCommand(
			request.Name,
			request.Description,
			request.SourceUrl,
			request.Ingredients.Select(i => i.ToDomain()).ToList());

		var result = await handler.HandleAsync(command, cancellationToken);
		return result.Match(
			onSuccess: recipe =>
			{
				var response = RecipeResponse.FromDomain(recipe);
				return Results.Created($"/api/recipes/{response.Id}", response);
			},
			onFailure: error => error.Code == ErrorCodes.ValidationFailed
				? Results.BadRequest(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> UpdateRecipe(
		string id,
		UpdateRecipeRequest request,
		ICommandHandler<UpdateRecipeCommand, Recipe> handler,
		CancellationToken cancellationToken)
	{
		var command = new UpdateRecipeCommand(
			id,
			request.Name,
			request.Description,
			request.SourceUrl,
			request.Ingredients.Select(i => i.ToDomain()).ToList());

		var result = await handler.HandleAsync(command, cancellationToken);
		return result.Match(
			onSuccess: recipe => Results.Ok(RecipeResponse.FromDomain(recipe)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}
}
