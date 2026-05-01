using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MealPlanner.Api.Features.Auth;
using MealPlanner.Api.Features.Catalogue.Commands;
using MealPlanner.Api.Features.Catalogue.Dtos;
using MealPlanner.Api.Features.Catalogue.Models;
using MealPlanner.Api.Features.Catalogue.Queries;
using MealPlanner.Api.Features.Recipes.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Catalogue;

/// <summary>
/// Public (user-role) catalogue endpoints used by the My Recipes browse modal.
/// </summary>
public static class CatalogueEndpoints
{
	public static IEndpointRouteBuilder MapCatalogueEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/catalogue")
			.WithTags("Catalogue")
			.RequireAuthorization(RbacAuthorization.RequireUserRolePolicy);

		group.MapGet("/", BrowseCatalogue);
		group.MapGet("/tags", GetTags);
		group.MapGet("/{id:guid}", GetById);
		group.MapPost("/{id:guid}/add-to-my-recipes", AddToMyRecipes);

		return app;
	}

	private static string? GetUserId(HttpContext httpContext) =>
		httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
		?? httpContext.User.FindFirst("sub")?.Value;

	private static async Task<IResult> BrowseCatalogue(
		HttpContext httpContext,
		IQueryHandler<BrowseCatalogueQuery, IReadOnlyList<CatalogueRecipeListItem>> handler,
		CancellationToken cancellationToken,
		string? search = null,
		string? tags = null,
		string? sort = null,
		int skip = 0,
		int take = 24)
	{
		var userId = GetUserId(httpContext);
		if (userId is null) return Results.Unauthorized();

		var tagSlugs = string.IsNullOrWhiteSpace(tags)
			? Array.Empty<string>()
			: tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var sortValue = string.Equals(sort, "popular", StringComparison.OrdinalIgnoreCase)
			? CatalogueSort.Popular
			: CatalogueSort.Recent;

		var query = new BrowseCatalogueQuery(userId, search, tagSlugs, sortValue, skip, take);
		var result = await handler.HandleAsync(query, cancellationToken);
		return result.Match(
			onSuccess: items => Results.Ok(items.Select(CatalogueRecipeListItemResponse.FromDomain).ToList()),
			onFailure: error => Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> GetById(
		Guid id,
		HttpContext httpContext,
		IQueryHandler<GetCatalogueRecipeByIdQuery, (CatalogueRecipe Recipe, bool AlreadyAdded)> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null) return Results.Unauthorized();

		var result = await handler.HandleAsync(new GetCatalogueRecipeByIdQuery(id, userId), cancellationToken);
		return result.Match(
			onSuccess: tuple => Results.Ok(CatalogueRecipeResponse.FromDomain(tuple.Recipe, tuple.AlreadyAdded)),
			onFailure: error => error.Code == ErrorCodes.NotFound
				? Results.NotFound(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> GetTags(
		IQueryHandler<GetCatalogueTagsQuery, IReadOnlyList<CatalogueTag>> handler,
		CancellationToken cancellationToken)
	{
		var result = await handler.HandleAsync(new GetCatalogueTagsQuery(), cancellationToken);
		return result.Match(
			onSuccess: tags => Results.Ok(tags.Select(CatalogueTagDto.FromDomain).ToList()),
			onFailure: error => Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> AddToMyRecipes(
		Guid id,
		HttpContext httpContext,
		ICommandHandler<AddCatalogueRecipeToUserCommand, Recipe> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null) return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new AddCatalogueRecipeToUserCommand(id, userId), cancellationToken);
		return result.Match(
			onSuccess: recipe => Results.Created(
				$"/api/recipes/{recipe.Id}",
				new { recipeId = recipe.Id }),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.ValidationFailed => Results.Conflict(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}
}
