using System.Security.Claims;
using MealPlanner.Api.Features.MealPlans.Commands;
using MealPlanner.Api.Features.MealPlans.Dtos;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Queries;
using MealPlanner.Api.Shared;

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

		return app;
	}

	private static string? GetUserId(HttpContext httpContext) =>
		httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
		?? httpContext.User.FindFirst("sub")?.Value;

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
		CancellationToken cancellationToken,
		string weekStart,
		string day,
		string category)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		if (!Enum.TryParse<DayOfWeek>(day, true, out var dayOfWeek))
			return Results.BadRequest($"Invalid day: {day}");

		if (!Enum.TryParse<MealCategory>(category, true, out var mealCategory))
			return Results.BadRequest($"Invalid category: {category}");

		var command = new UpdateDaySlotCommand(
			userId,
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
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

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
			userId,
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
		CancellationToken cancellationToken,
		string weekStart,
		string day,
		string category)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		if (!Enum.TryParse<DayOfWeek>(day, true, out var dayOfWeek))
			return Results.BadRequest($"Invalid day: {day}");

		if (!Enum.TryParse<MealCategory>(category, true, out var mealCategory))
			return Results.BadRequest($"Invalid category: {category}");

		var command = new RemoveSlotItemCommand(
			userId,
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
}
