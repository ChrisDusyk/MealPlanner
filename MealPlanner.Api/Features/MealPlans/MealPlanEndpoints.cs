using System.Security.Claims;
using MealPlanner.Api.Features.MealPlans.Commands;
using MealPlanner.Api.Features.MealPlans.Dtos;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Queries;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

namespace MealPlanner.Api.Features.MealPlans;

/// <summary>
/// Maps minimal API endpoints for meal plan management.
/// </summary>
public static class MealPlanEndpoints
{
	public static IEndpointRouteBuilder MapMealPlanEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/meal-plans")
			.WithTags("Meal Plans")
			.RequireAuthorization();

		group.MapGet("/", GetMealPlan);
		group.MapPut("/slots", UpdateDaySlot);
		group.MapPost("/copy-category", CopyCategory);
		group.MapDelete("/slots/{itemIndex:int}", RemoveSlotItem);

		// Sharing endpoints
		group.MapPost("/shares", ShareMealPlan);
		group.MapGet("/shares", GetSharesForMealPlan);
		group.MapDelete("/shares/{shareId}", RevokeMealPlanShare);
		group.MapGet("/shared-with-me", GetSharedWithMe);
		group.MapPost("/shared-with-me/{shareId}/dismiss", DismissSharedMealPlan);

		return app;
	}

	private static string? GetUserId(HttpContext httpContext) =>
		httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
		?? httpContext.User.FindFirst("sub")?.Value;

	/// <summary>
	/// Resolves the effective plan owner for mutation endpoints.
	/// If onBehalfOf is provided, verifies the caller has a ReadWrite share for that plan.
	/// Returns (effectiveUserId, errorResult) — errorResult is non-null if access is denied.
	/// </summary>
	private static async Task<(string? EffectiveUserId, IResult? Error)> ResolveEffectiveOwner(
		string callerId,
		string weekStart,
		string? onBehalfOf,
		IMongoClient mongoClient)
	{
		if (string.IsNullOrWhiteSpace(onBehalfOf))
			return (callerId, null);

		// Caller is trying to edit someone else's plan — verify ReadWrite share
		var sharesCollection = mongoClient
			.GetDatabase("mealplannerDb")
			.GetCollection<MealPlanShareDocument>("shares");

		var filter = MongoDB.Driver.Builders<MealPlanShareDocument>.Filter.And(
			MongoDB.Driver.Builders<MealPlanShareDocument>.Filter.Eq(s => s.OwnerUserId, onBehalfOf),
			MongoDB.Driver.Builders<MealPlanShareDocument>.Filter.Eq(s => s.SharedWithUserId, callerId),
			MongoDB.Driver.Builders<MealPlanShareDocument>.Filter.Eq(s => s.WeekStart, weekStart),
			MongoDB.Driver.Builders<MealPlanShareDocument>.Filter.Eq(s => s.Permission,
				nameof(SharePermission.ReadWrite)),
			MongoDB.Driver.Builders<MealPlanShareDocument>.Filter.Eq(s => s.DismissedByRecipient, false));

		var share = await sharesCollection.Find(filter).FirstOrDefaultAsync();

		if (share is null)
			return (null, Results.Forbid());

		return (onBehalfOf, null);
	}

	private static async Task<IResult> GetMealPlan(
		HttpContext httpContext,
		IQueryHandler<GetMealPlanQuery, MealPlan> handler,
		CancellationToken cancellationToken,
		string? weekStart = null)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var week = string.IsNullOrEmpty(weekStart)
			? DateOnly.FromDateTime(DateTime.UtcNow)
			: DateOnly.ParseExact(weekStart, "yyyy-MM-dd");

		var result = await handler.HandleAsync(new GetMealPlanQuery(userId, week), cancellationToken);
		return result.Match(
			onSuccess: plan => Results.Ok(MealPlanResponse.FromDomain(plan)),
			onFailure: error => Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> UpdateDaySlot(
		HttpContext httpContext,
		UpdateDaySlotRequest request,
		ICommandHandler<UpdateDaySlotCommand, MealPlan> handler,
		IMongoClient mongoClient,
		CancellationToken cancellationToken,
		string weekStart,
		string day,
		string category,
		string? onBehalfOf = null)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var (effectiveUserId, accessError) = await ResolveEffectiveOwner(userId, weekStart, onBehalfOf, mongoClient);
		if (accessError is not null) return accessError;

		if (!Enum.TryParse<DayOfWeek>(day, true, out var dayOfWeek))
			return Results.BadRequest($"Invalid day: {day}");

		if (!Enum.TryParse<MealCategory>(category, true, out var mealCategory))
			return Results.BadRequest($"Invalid category: {category}");

		var command = new UpdateDaySlotCommand(
			effectiveUserId!,
			DateOnly.ParseExact(weekStart, "yyyy-MM-dd"),
			dayOfWeek,
			mealCategory,
			request.Items.Select(i => i.ToDomain()).ToList());

		var result = await handler.HandleAsync(command, cancellationToken);
		return result.Match(
			onSuccess: plan => Results.Ok(MealPlanResponse.FromDomain(plan)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> CopyCategory(
		HttpContext httpContext,
		CopyCategoryRequest request,
		ICommandHandler<CopyCategoryCommand, MealPlan> handler,
		IMongoClient mongoClient,
		CancellationToken cancellationToken,
		string weekStart,
		string? onBehalfOf = null)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var (effectiveUserId, accessError) = await ResolveEffectiveOwner(userId, weekStart, onBehalfOf, mongoClient);
		if (accessError is not null) return accessError;

		if (!Enum.TryParse<DayOfWeek>(request.SourceDay, true, out var sourceDay))
			return Results.BadRequest($"Invalid source day: {request.SourceDay}");

		if (!Enum.TryParse<MealCategory>(request.Category, true, out var category))
			return Results.BadRequest($"Invalid category: {request.Category}");

		var targetDays = new List<DayOfWeek>();
		foreach (var td in request.TargetDays)
		{
			if (!Enum.TryParse<DayOfWeek>(td, true, out var parsed))
				return Results.BadRequest($"Invalid target day: {td}");
			targetDays.Add(parsed);
		}

		var command = new CopyCategoryCommand(
			effectiveUserId!,
			DateOnly.ParseExact(weekStart, "yyyy-MM-dd"),
			sourceDay,
			category,
			targetDays);

		var result = await handler.HandleAsync(command, cancellationToken);
		return result.Match(
			onSuccess: plan => Results.Ok(MealPlanResponse.FromDomain(plan)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> RemoveSlotItem(
		int itemIndex,
		HttpContext httpContext,
		ICommandHandler<RemoveSlotItemCommand, MealPlan> handler,
		IMongoClient mongoClient,
		CancellationToken cancellationToken,
		string weekStart,
		string day,
		string category,
		string? onBehalfOf = null)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var (effectiveUserId, accessError) = await ResolveEffectiveOwner(userId, weekStart, onBehalfOf, mongoClient);
		if (accessError is not null) return accessError;

		if (!Enum.TryParse<DayOfWeek>(day, true, out var dayOfWeek))
			return Results.BadRequest($"Invalid day: {day}");

		if (!Enum.TryParse<MealCategory>(category, true, out var mealCategory))
			return Results.BadRequest($"Invalid category: {category}");

		var command = new RemoveSlotItemCommand(
			effectiveUserId!,
			DateOnly.ParseExact(weekStart, "yyyy-MM-dd"),
			dayOfWeek,
			mealCategory,
			itemIndex);

		var result = await handler.HandleAsync(command, cancellationToken);
		return result.Match(
			onSuccess: plan => Results.Ok(MealPlanResponse.FromDomain(plan)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	// ── Sharing Handlers ───────────────────────────────────

	private static async Task<IResult> ShareMealPlan(
		ShareMealPlanRequest request,
		HttpContext httpContext,
		ICommandHandler<ShareMealPlanCommand, MealPlanShare> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		if (!Enum.TryParse<SharePermission>(request.Permission, true, out var permission))
			return Results.BadRequest($"Invalid permission: {request.Permission}. Use 'ReadOnly' or 'ReadWrite'.");

		var command = new ShareMealPlanCommand(userId, request.Email, request.WeekStart, permission);
		var result = await handler.HandleAsync(command, cancellationToken);
		return result.Match(
			onSuccess: share => Results.Created($"/api/meal-plans/shares/{share.Id}",
				MealPlanShareResponse.FromDomain(share)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> GetSharesForMealPlan(
		HttpContext httpContext,
		IQueryHandler<GetSharesForMealPlanQuery, List<ShareWithRecipientInfo>> handler,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new GetSharesForMealPlanQuery(userId, weekStart), cancellationToken);
		return result.Match(
			onSuccess: shares => Results.Ok(shares.Select(s =>
				MealPlanShareResponse.FromDomain(s.Share, s.RecipientName, s.RecipientEmail)).ToList()),
			onFailure: error => Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> RevokeMealPlanShare(
		string shareId,
		HttpContext httpContext,
		ICommandHandler<RevokeMealPlanShareCommand, Unit> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new RevokeMealPlanShareCommand(userId, shareId), cancellationToken);
		return result.Match(
			onSuccess: _ => Results.NoContent(),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> GetSharedWithMe(
		HttpContext httpContext,
		IQueryHandler<GetSharedWithMeQuery, List<SharedMealPlanResult>> handler,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new GetSharedWithMeQuery(userId, weekStart), cancellationToken);
		return result.Match(
			onSuccess: plans => Results.Ok(plans.Select(p => new SharedMealPlanResponse(
				ShareId: p.Share.Id,
				OwnerName: p.OwnerName,
				OwnerEmail: p.OwnerEmail,
				Permission: p.Share.Permission.ToString(),
				MealPlan: MealPlanResponse.FromDomain(p.MealPlan)
			)).ToList()),
			onFailure: error => Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> DismissSharedMealPlan(
		string shareId,
		HttpContext httpContext,
		ICommandHandler<DismissSharedMealPlanCommand, Unit> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new DismissSharedMealPlanCommand(userId, shareId), cancellationToken);
		return result.Match(
			onSuccess: _ => Results.NoContent(),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}
}
