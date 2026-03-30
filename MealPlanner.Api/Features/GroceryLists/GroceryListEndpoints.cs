using System.Security.Claims;
using MealPlanner.Api.Features.Auth;
using MealPlanner.Api.Features.GroceryLists.Commands;
using MealPlanner.Api.Features.GroceryLists.Dtos;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.GroceryLists.Queries;
using MealPlanner.Api.Features.GroceryLists.Realtime;
using MealPlanner.Api.Features.MealPlans.Models;
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
			.RequireAuthorization(RbacAuthorization.RequireUserRolePolicy);

		group.MapPost("/generate", GenerateGroceryList);
		group.MapGet("/", GetGroceryList);
		group.MapPut("/items/{itemIndex:int}/toggle", ToggleGroceryListItem);
		group.MapPost("/items", AddCustomItem);
		group.MapPost("/pantry-staples/{itemIndex:int}/promote", PromotePantryStapleItem);
		group.MapDelete("/", DeleteGroceryList);

		// Sharing endpoints
		group.MapPost("/shares", ShareGroceryList);
		group.MapGet("/shares", GetSharesForGroceryList);
		group.MapDelete("/shares/{shareId}", RevokeGroceryListShare);
		group.MapGet("/shared-with-me", GetGroceryListsSharedWithMe);
		group.MapPost("/shared-with-me/{shareId}/dismiss", DismissSharedGroceryList);

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

		if (!DateOnly.TryParseExact(weekStart, "yyyy-MM-dd", out var week))
			return Results.BadRequest("weekStart must be a valid date in yyyy-MM-dd format.");
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

		if (!DateOnly.TryParseExact(weekStart, "yyyy-MM-dd", out var week))
			return Results.BadRequest("weekStart must be a valid date in yyyy-MM-dd format.");
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
		IGroceryListRealtimeNotifier realtimeNotifier,
		CancellationToken cancellationToken,
		string weekStart,
		string? ownerUserId = null)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		if (!DateOnly.TryParseExact(weekStart, "yyyy-MM-dd", out var week))
			return Results.BadRequest("weekStart must be a valid date in yyyy-MM-dd format.");
		var result = await handler.HandleAsync(
			new ToggleGroceryListItemCommand(userId, week, itemIndex, ownerUserId), cancellationToken);

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
			ownerUserId: list.UserId,
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
		CancellationToken cancellationToken,
		string weekStart,
		string? ownerUserId = null)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		if (!DateOnly.TryParseExact(weekStart, "yyyy-MM-dd", out var week))
			return Results.BadRequest("weekStart must be a valid date in yyyy-MM-dd format.");
		var result = await handler.HandleAsync(
			new AddCustomItemCommand(userId, week, request.Name, ownerUserId), cancellationToken);

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
			ownerUserId: list.UserId,
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
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		if (!DateOnly.TryParseExact(weekStart, "yyyy-MM-dd", out var week))
			return Results.BadRequest("weekStart must be a valid date in yyyy-MM-dd format.");
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

	private static async Task<IResult> PromotePantryStapleItem(
		int itemIndex,
		HttpContext httpContext,
		ICommandHandler<PromotePantryStapleItemCommand, GroceryList> handler,
		IGroceryListRealtimeNotifier realtimeNotifier,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		if (!DateOnly.TryParseExact(weekStart, "yyyy-MM-dd", out var week))
			return Results.BadRequest("weekStart must be a valid date in yyyy-MM-dd format.");

		var result = await handler.HandleAsync(
			new PromotePantryStapleItemCommand(userId, week, itemIndex), cancellationToken);

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
			ownerUserId: list.UserId,
			weekStart: list.WeekStart,
			updatedList: list,
			changedByUserId: userId,
			eventType: GroceryListRealtimeEventType.PantryStaplePromoted,
			cancellationToken: cancellationToken);

		return Results.Ok(GroceryListResponse.FromDomain(list));
	}

	// ── Sharing Handlers ───────────────────────────────────

	private static async Task<IResult> ShareGroceryList(
		ShareGroceryListRequest request,
		HttpContext httpContext,
		ICommandHandler<ShareGroceryListCommand, GroceryListShare> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		if (!Enum.TryParse<SharePermission>(request.Permission, true, out var permission))
			return Results.BadRequest($"Invalid permission: {request.Permission}. Use 'ReadOnly' or 'ReadWrite'.");

		var command = new ShareGroceryListCommand(userId, request.Email, request.WeekStart, permission);
		var result = await handler.HandleAsync(command, cancellationToken);
		return result.Match(
			onSuccess: share => Results.Created($"/api/grocery-lists/shares/{share.Id}",
				GroceryListShareResponse.FromDomain(share)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> GetSharesForGroceryList(
		HttpContext httpContext,
		IQueryHandler<GetSharesForGroceryListQuery, List<GroceryListShareWithRecipientInfo>> handler,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new GetSharesForGroceryListQuery(userId, weekStart), cancellationToken);
		return result.Match(
			onSuccess: shares => Results.Ok(shares.Select(s =>
				GroceryListShareResponse.FromDomain(s.Share, s.RecipientName, s.RecipientEmail)).ToList()),
			onFailure: error => Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> RevokeGroceryListShare(
		string shareId,
		HttpContext httpContext,
		ICommandHandler<RevokeGroceryListShareCommand, Unit> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new RevokeGroceryListShareCommand(userId, shareId), cancellationToken);
		return result.Match(
			onSuccess: _ => Results.NoContent(),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> GetGroceryListsSharedWithMe(
		HttpContext httpContext,
		IQueryHandler<GetGroceryListsSharedWithMeQuery, List<SharedGroceryListResult>> handler,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new GetGroceryListsSharedWithMeQuery(userId, weekStart), cancellationToken);
		return result.Match(
			onSuccess: lists => Results.Ok(lists.Select(l => new SharedGroceryListResponse(
				ShareId: l.Share.Id,
				OwnerUserId: l.Share.OwnerUserId,
				OwnerName: l.OwnerName,
				OwnerEmail: l.OwnerEmail,
				Permission: l.Share.Permission.ToString(),
				GroceryList: GroceryListResponse.FromDomain(l.GroceryList)
			)).ToList()),
			onFailure: error => Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> DismissSharedGroceryList(
		string shareId,
		HttpContext httpContext,
		ICommandHandler<DismissSharedGroceryListCommand, Unit> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new DismissSharedGroceryListCommand(userId, shareId), cancellationToken);
		return result.Match(
			onSuccess: _ => Results.NoContent(),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}
}
