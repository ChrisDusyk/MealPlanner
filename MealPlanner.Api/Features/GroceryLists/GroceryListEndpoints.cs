using System.Security.Claims;
using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Features.GroceryLists.Dtos;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.GroceryLists.Queries;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.GroceryLists;

/// <summary>
/// Maps minimal API endpoints for grocery list management.
/// </summary>
public static class GroceryListEndpoints
{
	public static IEndpointRouteBuilder MapGroceryListEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/grocery-lists")
			.WithTags("Grocery Lists")
			.RequireAuthorization();

		group.MapPost("/generate", GenerateGroceryList);
		group.MapGet("/", GetGroceryList);
		group.MapPut("/items/{itemIndex:int}/toggle", ToggleGroceryListItem);
		group.MapPost("/items", AddCustomItem);
		group.MapDelete("/", DeleteGroceryList);

		return app;
	}

	private static string? GetUserId(HttpContext httpContext) =>
		httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
		?? httpContext.User.FindFirst("sub")?.Value;

	private static async Task<IResult> GenerateGroceryList(
		HttpContext httpContext,
		ICommandHandler<GenerateGroceryListCommand, GroceryList> handler,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var week = DateOnly.ParseExact(weekStart, "yyyy-MM-dd");
		var result = await handler.HandleAsync(new GenerateGroceryListCommand(userId, week), cancellationToken);
		return result.Match(
			onSuccess: list => Results.Ok(GroceryListResponse.FromDomain(list)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> GetGroceryList(
		HttpContext httpContext,
		IQueryHandler<GetGroceryListQuery, GroceryList> handler,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var week = DateOnly.ParseExact(weekStart, "yyyy-MM-dd");
		var result = await handler.HandleAsync(new GetGroceryListQuery(userId, week), cancellationToken);
		return result.Match(
			onSuccess: list => Results.Ok(GroceryListResponse.FromDomain(list)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> ToggleGroceryListItem(
		int itemIndex,
		HttpContext httpContext,
		ICommandHandler<ToggleGroceryListItemCommand, GroceryList> handler,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var week = DateOnly.ParseExact(weekStart, "yyyy-MM-dd");
		var result = await handler.HandleAsync(
			new ToggleGroceryListItemCommand(userId, week, itemIndex), cancellationToken);
		return result.Match(
			onSuccess: list => Results.Ok(GroceryListResponse.FromDomain(list)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> AddCustomItem(
		AddCustomItemRequest request,
		HttpContext httpContext,
		ICommandHandler<AddCustomItemCommand, GroceryList> handler,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var week = DateOnly.ParseExact(weekStart, "yyyy-MM-dd");
		var result = await handler.HandleAsync(
			new AddCustomItemCommand(userId, week, request.Name), cancellationToken);
		return result.Match(
			onSuccess: list => Results.Ok(GroceryListResponse.FromDomain(list)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> DeleteGroceryList(
		HttpContext httpContext,
		ICommandHandler<DeleteGroceryListCommand, Unit> handler,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var week = DateOnly.ParseExact(weekStart, "yyyy-MM-dd");
		var result = await handler.HandleAsync(
			new DeleteGroceryListCommand(userId, week), cancellationToken);
		return result.Match(
			onSuccess: _ => Results.NoContent(),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}
}
