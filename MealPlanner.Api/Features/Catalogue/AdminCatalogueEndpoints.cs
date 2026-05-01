using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MealPlanner.Api.Features.Auth;
using MealPlanner.Api.Features.Catalogue.Commands;
using MealPlanner.Api.Features.Catalogue.Dtos;
using MealPlanner.Api.Features.Catalogue.Models;
using MealPlanner.Api.Features.Catalogue.Queries;
using MealPlanner.Api.Features.Recipes.Dtos;
using MealPlanner.Api.Features.Recipes.Import;
using MealPlanner.Api.Features.Recipes.Queries;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Catalogue;

/// <summary>
/// Admin-only catalogue management endpoints (CRUD + tag admin + URL import).
/// </summary>
public static class AdminCatalogueEndpoints
{
	public static IEndpointRouteBuilder MapAdminCatalogueEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/admin/catalogue")
			.WithTags("Admin Catalogue")
			.RequireAuthorization(RbacAuthorization.RequireAdminRolePolicy);

		group.MapGet("/", ListAll);
		group.MapGet("/{id:guid}", GetById);
		group.MapPost("/", Create);
		group.MapPut("/{id:guid}", Update);
		group.MapPut("/{id:guid}/published", SetPublished);
		group.MapDelete("/{id:guid}", Delete);
		group.MapPost("/import-from-url", ImportFromUrl);

		var tagGroup = app.MapGroup("/api/admin/catalogue/tags")
			.WithTags("Admin Catalogue Tags")
			.RequireAuthorization(RbacAuthorization.RequireAdminRolePolicy);

		tagGroup.MapGet("/", ListTags);
		tagGroup.MapPost("/", CreateTag);
		tagGroup.MapPut("/{id:guid}", UpdateTag);
		tagGroup.MapDelete("/{id:guid}", DeleteTag);

		return app;
	}

	private static string? GetUserId(HttpContext httpContext) =>
		httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
		?? httpContext.User.FindFirst("sub")?.Value;

	private static Dictionary<string, string[]> ValidateRequest(object request)
	{
		var validationResults = new List<ValidationResult>();
		Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true);
		return validationResults
			.GroupBy(
				r => r.MemberNames.FirstOrDefault() ?? "request",
				r => r.ErrorMessage ?? "Invalid value")
			.ToDictionary(g => g.Key, g => g.Distinct().ToArray());
	}

	private static async Task<IResult> ListAll(
		IQueryHandler<ListAllCatalogueRecipesQuery, IReadOnlyList<CatalogueRecipe>> handler,
		CancellationToken cancellationToken,
		string? search = null,
		bool? isPublished = null)
	{
		var result = await handler.HandleAsync(new ListAllCatalogueRecipesQuery(search, isPublished), cancellationToken);
		return result.Match(
			onSuccess: items => Results.Ok(items
				.Select(r => CatalogueRecipeResponse.FromDomain(r, alreadyAdded: false))
				.ToList()),
			onFailure: error => Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> GetById(
		Guid id,
		IQueryHandler<GetCatalogueRecipeForAdminQuery, CatalogueRecipe> handler,
		CancellationToken cancellationToken)
	{
		var result = await handler.HandleAsync(new GetCatalogueRecipeForAdminQuery(id), cancellationToken);
		return result.Match(
			onSuccess: recipe => Results.Ok(CatalogueRecipeResponse.FromDomain(recipe, alreadyAdded: false)),
			onFailure: error => error.Code == ErrorCodes.NotFound
				? Results.NotFound(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> Create(
		CreateCatalogueRecipeRequest request,
		HttpContext httpContext,
		ICommandHandler<CreateCatalogueRecipeCommand, CatalogueRecipe> handler,
		CancellationToken cancellationToken)
	{
		var errors = ValidateRequest(request);
		if (errors.Count > 0) return Results.ValidationProblem(errors);

		var userId = GetUserId(httpContext);
		if (userId is null) return Results.Unauthorized();

		var command = new CreateCatalogueRecipeCommand(
			userId,
			request.Name,
			request.Description,
			request.Servings,
			Option<string>.From(request.SourceUrl),
			Option<string>.From(request.ImageUrl),
			request.Ingredients.Select(i => i.ToDomain()).ToList(),
			request.TagIds,
			request.IsPublished);

		var result = await handler.HandleAsync(command, cancellationToken);
		return result.Match(
			onSuccess: recipe =>
			{
				var response = CatalogueRecipeResponse.FromDomain(recipe, alreadyAdded: false);
				return Results.Created($"/api/admin/catalogue/{response.Id}", response);
			},
			onFailure: error => error.Code == ErrorCodes.ValidationFailed
				? Results.BadRequest(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> Update(
		Guid id,
		UpdateCatalogueRecipeRequest request,
		ICommandHandler<UpdateCatalogueRecipeCommand, CatalogueRecipe> handler,
		CancellationToken cancellationToken)
	{
		var errors = ValidateRequest(request);
		if (errors.Count > 0) return Results.ValidationProblem(errors);

		var command = new UpdateCatalogueRecipeCommand(
			id,
			request.Name,
			request.Description,
			request.Servings,
			Option<string>.From(request.SourceUrl),
			Option<string>.From(request.ImageUrl),
			request.Ingredients.Select(i => i.ToDomain()).ToList(),
			request.TagIds);

		var result = await handler.HandleAsync(command, cancellationToken);
		return result.Match(
			onSuccess: recipe => Results.Ok(CatalogueRecipeResponse.FromDomain(recipe, alreadyAdded: false)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> SetPublished(
		Guid id,
		SetCatalogueRecipePublishedRequest request,
		ICommandHandler<SetCatalogueRecipePublishedCommand, CatalogueRecipe> handler,
		CancellationToken cancellationToken)
	{
		var result = await handler.HandleAsync(
			new SetCatalogueRecipePublishedCommand(id, request.IsPublished), cancellationToken);
		return result.Match(
			onSuccess: recipe => Results.Ok(CatalogueRecipeResponse.FromDomain(recipe, alreadyAdded: false)),
			onFailure: error => error.Code == ErrorCodes.NotFound
				? Results.NotFound(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> Delete(
		Guid id,
		ICommandHandler<DeleteCatalogueRecipeCommand, Unit> handler,
		CancellationToken cancellationToken)
	{
		var result = await handler.HandleAsync(new DeleteCatalogueRecipeCommand(id), cancellationToken);
		return result.Match(
			onSuccess: _ => Results.NoContent(),
			onFailure: error => error.Code == ErrorCodes.NotFound
				? Results.NotFound(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> ImportFromUrl(
		ImportIngredientsRequest request,
		IQueryHandler<ImportIngredientsQuery, ImportedIngredientSet> handler,
		CancellationToken cancellationToken)
	{
		var result = await handler.HandleAsync(new ImportIngredientsQuery(request.SourceUrl), cancellationToken);
		return result.Match(
			onSuccess: importResult => Results.Ok(new ImportIngredientsResponse(
				importResult.Ingredients.Select(IngredientDto.FromDomain).ToList(),
				importResult.Warnings.ToList(),
				importResult.RecipeName.GetValueOrNull(),
				importResult.Servings.Match(s => (int?)s, () => null))),
			onFailure: error => error.Code == ErrorCodes.ValidationFailed
				? Results.BadRequest(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	// ---- Tag endpoints ----

	private static async Task<IResult> ListTags(
		IQueryHandler<GetCatalogueTagsQuery, IReadOnlyList<CatalogueTag>> handler,
		CancellationToken cancellationToken)
	{
		var result = await handler.HandleAsync(new GetCatalogueTagsQuery(), cancellationToken);
		return result.Match(
			onSuccess: tags => Results.Ok(tags.Select(CatalogueTagDto.FromDomain).ToList()),
			onFailure: error => Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> CreateTag(
		CatalogueTagRequest request,
		ICommandHandler<CreateCatalogueTagCommand, CatalogueTag> handler,
		CancellationToken cancellationToken)
	{
		var errors = ValidateRequest(request);
		if (errors.Count > 0) return Results.ValidationProblem(errors);

		var result = await handler.HandleAsync(
			new CreateCatalogueTagCommand(request.Slug, request.Name), cancellationToken);
		return result.Match(
			onSuccess: tag => Results.Created($"/api/admin/catalogue/tags/{tag.Id}", CatalogueTagDto.FromDomain(tag)),
			onFailure: error => error.Code == ErrorCodes.ValidationFailed
				? Results.BadRequest(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> UpdateTag(
		Guid id,
		CatalogueTagRequest request,
		ICommandHandler<UpdateCatalogueTagCommand, CatalogueTag> handler,
		CancellationToken cancellationToken)
	{
		var errors = ValidateRequest(request);
		if (errors.Count > 0) return Results.ValidationProblem(errors);

		var result = await handler.HandleAsync(
			new UpdateCatalogueTagCommand(id, request.Slug, request.Name), cancellationToken);
		return result.Match(
			onSuccess: tag => Results.Ok(CatalogueTagDto.FromDomain(tag)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> DeleteTag(
		Guid id,
		ICommandHandler<DeleteCatalogueTagCommand, Unit> handler,
		CancellationToken cancellationToken)
	{
		var result = await handler.HandleAsync(new DeleteCatalogueTagCommand(id), cancellationToken);
		return result.Match(
			onSuccess: _ => Results.NoContent(),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.ValidationFailed => Results.Conflict(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}
}
