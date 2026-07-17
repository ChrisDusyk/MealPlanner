using System.Security.Claims;
using MealPlanner.Api.Features.Auth;
using MealPlanner.Api.Features.Families;
using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Features.GroceryLists.Dtos;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.GroceryLists.Queries;
using MealPlanner.Api.Features.GroceryLists.Realtime;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.GroceryLists;

/// <summary>
/// Maps minimal API endpoints for grocery list management. The caller's
/// family group (resolved from their auth user id) owns the list; every
/// family member has full access.
/// </summary>
public static class GroceryListEndpoints
{
	public static IEndpointRouteBuilder MapGroceryListEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/grocery-lists")
			.WithTags("Grocery Lists")
			.RequireAuthorization(RbacAuthorization.RequireUserRolePolicy);

		group.MapPost("/generate", GenerateGroceryList);
		group.MapGet("/", GetGroceryList);
		group.MapPut("/items/{itemIndex:int}/toggle", ToggleGroceryListItem);
		group.MapPost("/items", AddCustomItem);
		group.MapPost("/pantry-staples/{itemIndex:int}/promote", PromotePantryStapleItem);
		group.MapDelete("/", DeleteGroceryList);

		return app;
	}

	private static string? GetUserId(HttpContext httpContext) =>
		httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
		?? httpContext.User.FindFirst("sub")?.Value;

	private static async Task<IResult> GenerateGroceryList(
		HttpContext httpContext,
		ICommandHandler<GenerateGroceryListCommand, GroceryList> handler,
		IFamilyContextResolver familyResolver,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		if (!DateOnly.TryParseExact(weekStart, "yyyy-MM-dd", out var week))
			return Results.BadRequest("weekStart must be a valid date in yyyy-MM-dd format.");

		var familyResult = await familyResolver.ResolveAsync(userId, cancellationToken);
		if (!familyResult.IsSuccess)
			return Results.Problem(familyResult.Error!.Message, statusCode: 500);

		var result = await handler.HandleAsync(
			new GenerateGroceryListCommand(familyResult.Value!.FamilyGroupId, week), cancellationToken);
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
		IFamilyContextResolver familyResolver,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		if (!DateOnly.TryParseExact(weekStart, "yyyy-MM-dd", out var week))
			return Results.BadRequest("weekStart must be a valid date in yyyy-MM-dd format.");

		var familyResult = await familyResolver.ResolveAsync(userId, cancellationToken);
		if (!familyResult.IsSuccess)
			return Results.Problem(familyResult.Error!.Message, statusCode: 500);

		var result = await handler.HandleAsync(
			new GetGroceryListQuery(familyResult.Value!.FamilyGroupId, week), cancellationToken);
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
		IGroceryListRealtimeNotifier realtimeNotifier,
		IFamilyContextResolver familyResolver,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		if (!DateOnly.TryParseExact(weekStart, "yyyy-MM-dd", out var week))
			return Results.BadRequest("weekStart must be a valid date in yyyy-MM-dd format.");

		var familyResult = await familyResolver.ResolveAsync(userId, cancellationToken);
		if (!familyResult.IsSuccess)
			return Results.Problem(familyResult.Error!.Message, statusCode: 500);
		var familyGroupId = familyResult.Value!.FamilyGroupId;

		var result = await handler.HandleAsync(
			new ToggleGroceryListItemCommand(familyGroupId, week, itemIndex), cancellationToken);

		if (!result.IsSuccess)
		{
			var error = result.Error!;
			return error.Code switch
			{
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			};
		}

		var list = result.Value!;
		await realtimeNotifier.PublishListUpdatedAsync(
			familyGroupId: familyGroupId,
			weekStart: list.WeekStart,
			updatedList: list,
			changedByUserId: userId,
			eventType: GroceryListRealtimeEventType.ItemToggled,
			cancellationToken: cancellationToken);

		return Results.Ok(GroceryListResponse.FromDomain(list));
	}

	private static async Task<IResult> AddCustomItem(
		AddCustomItemRequest request,
		HttpContext httpContext,
		ICommandHandler<AddCustomItemCommand, GroceryList> handler,
		IGroceryListRealtimeNotifier realtimeNotifier,
		IFamilyContextResolver familyResolver,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		if (!DateOnly.TryParseExact(weekStart, "yyyy-MM-dd", out var week))
			return Results.BadRequest("weekStart must be a valid date in yyyy-MM-dd format.");

		var familyResult = await familyResolver.ResolveAsync(userId, cancellationToken);
		if (!familyResult.IsSuccess)
			return Results.Problem(familyResult.Error!.Message, statusCode: 500);
		var familyGroupId = familyResult.Value!.FamilyGroupId;

		var result = await handler.HandleAsync(
			new AddCustomItemCommand(familyGroupId, week, request.Name), cancellationToken);

		if (!result.IsSuccess)
		{
			var error = result.Error!;
			return error.Code switch
			{
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			};
		}

		var list = result.Value!;
		await realtimeNotifier.PublishListUpdatedAsync(
			familyGroupId: familyGroupId,
			weekStart: list.WeekStart,
			updatedList: list,
			changedByUserId: userId,
			eventType: GroceryListRealtimeEventType.CustomItemAdded,
			cancellationToken: cancellationToken);

		return Results.Ok(GroceryListResponse.FromDomain(list));
	}

	private static async Task<IResult> DeleteGroceryList(
		HttpContext httpContext,
		ICommandHandler<DeleteGroceryListCommand, Unit> handler,
		IFamilyContextResolver familyResolver,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		if (!DateOnly.TryParseExact(weekStart, "yyyy-MM-dd", out var week))
			return Results.BadRequest("weekStart must be a valid date in yyyy-MM-dd format.");

		var familyResult = await familyResolver.ResolveAsync(userId, cancellationToken);
		if (!familyResult.IsSuccess)
			return Results.Problem(familyResult.Error!.Message, statusCode: 500);

		var result = await handler.HandleAsync(
			new DeleteGroceryListCommand(familyResult.Value!.FamilyGroupId, week), cancellationToken);
		return result.Match(
			onSuccess: _ => Results.NoContent(),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> PromotePantryStapleItem(
		int itemIndex,
		HttpContext httpContext,
		ICommandHandler<PromotePantryStapleItemCommand, GroceryList> handler,
		IGroceryListRealtimeNotifier realtimeNotifier,
		IFamilyContextResolver familyResolver,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		if (!DateOnly.TryParseExact(weekStart, "yyyy-MM-dd", out var week))
			return Results.BadRequest("weekStart must be a valid date in yyyy-MM-dd format.");

		var familyResult = await familyResolver.ResolveAsync(userId, cancellationToken);
		if (!familyResult.IsSuccess)
			return Results.Problem(familyResult.Error!.Message, statusCode: 500);
		var familyGroupId = familyResult.Value!.FamilyGroupId;

		var result = await handler.HandleAsync(
			new PromotePantryStapleItemCommand(familyGroupId, week, itemIndex), cancellationToken);

		if (!result.IsSuccess)
		{
			var error = result.Error!;
			return error.Code switch
			{
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			};
		}

		var list = result.Value!;
		await realtimeNotifier.PublishListUpdatedAsync(
			familyGroupId: familyGroupId,
			weekStart: list.WeekStart,
			updatedList: list,
			changedByUserId: userId,
			eventType: GroceryListRealtimeEventType.PantryStaplePromoted,
			cancellationToken: cancellationToken);

		return Results.Ok(GroceryListResponse.FromDomain(list));
	}
}
