using System.Security.Claims;
using MealPlanner.Api.Features.Recipes.Commands;
using MealPlanner.Api.Features.Recipes.Dtos;
using MealPlanner.Api.Features.Recipes.Import;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Features.Recipes.Queries;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Recipes;

/// <summary>
/// Maps minimal API endpoints for recipe management.
/// </summary>
public static class RecipeEndpoints
{
	public const string ImportIngredientsRateLimitPolicy = "ImportIngredientsPerUser";

	public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/recipes")
			.WithTags("Recipes")
			.RequireAuthorization();

		group.MapGet("/", GetAllRecipes);
		group.MapGet("/{id}", GetRecipeById);
		group.MapPost("/import-ingredients", ImportIngredients)
			.RequireRateLimiting(ImportIngredientsRateLimitPolicy);
		group.MapPost("/", CreateRecipe);
		group.MapPut("/{id}", UpdateRecipe);

		return app;
	}

	private static string? GetUserId(HttpContext httpContext) =>
		httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
		?? httpContext.User.FindFirst("sub")?.Value;

	private static async Task<IResult> GetAllRecipes(
		HttpContext httpContext,
		IQueryHandler<GetAllRecipesQuery, IReadOnlyList<Recipe>> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(new GetAllRecipesQuery(userId), cancellationToken);
		return result.Match(
			onSuccess: recipes => Results.Ok(recipes.Select(RecipeResponse.FromDomain).ToList()),
			onFailure: error => Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> GetRecipeById(
		string id,
		HttpContext httpContext,
		IQueryHandler<GetRecipeByIdQuery, Recipe> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(new GetRecipeByIdQuery(id, userId), cancellationToken);
		return result.Match(
			onSuccess: recipe => Results.Ok(RecipeResponse.FromDomain(recipe)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.Unauthorized => Results.Forbid(),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> CreateRecipe(
		CreateRecipeRequest request,
		HttpContext httpContext,
		ICommandHandler<CreateRecipeCommand, Recipe> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var command = new CreateRecipeCommand(
			userId,
			request.Name,
			request.Description,
			Option<string>.From(request.SourceUrl),
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

	private static async Task<IResult> ImportIngredients(
		ImportIngredientsRequest request,
		HttpContext httpContext,
		IQueryHandler<ImportIngredientsQuery, ImportedIngredientSet> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(new ImportIngredientsQuery(request.SourceUrl), cancellationToken);
		return result.Match(
			onSuccess: importResult => Results.Ok(new ImportIngredientsResponse(
				importResult.Ingredients.Select(IngredientDto.FromDomain).ToList(),
				importResult.Warnings.ToList())),
			onFailure: error => error.Code == ErrorCodes.ValidationFailed
				? Results.BadRequest(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> UpdateRecipe(
		string id,
		UpdateRecipeRequest request,
		HttpContext httpContext,
		ICommandHandler<UpdateRecipeCommand, Recipe> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var command = new UpdateRecipeCommand(
			id,
			userId,
			request.Name,
			request.Description,
			Option<string>.From(request.SourceUrl),
			request.Ingredients.Select(i => i.ToDomain()).ToList());

		var result = await handler.HandleAsync(command, cancellationToken);
		return result.Match(
			onSuccess: recipe => Results.Ok(RecipeResponse.FromDomain(recipe)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.Unauthorized => Results.Forbid(),
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}
}

