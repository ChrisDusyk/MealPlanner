using System.Security.Claims;
using MealPlanner.Api.Features.Auth;
using MealPlanner.Api.Features.Families;
using MealPlanner.Api.Features.MealPlans.Commands;
using MealPlanner.Api.Features.MealPlans.Dtos;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Queries;
using MealPlanner.Api.Features.MealPlans.Realtime;
using MealPlanner.Api.Shared;
using System.ComponentModel.DataAnnotations;

namespace MealPlanner.Api.Features.MealPlans;

/// <summary>
/// Maps minimal API endpoints for meal plan management. The caller's family
/// group (resolved from their auth user id) owns the plan; every family
/// member has full access.
/// </summary>
public static class MealPlanEndpoints
{
	public static IEndpointRouteBuilder MapMealPlanEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/meal-plans")
			.WithTags("Meal Plans")
			.RequireAuthorization(RbacAuthorization.RequireUserRolePolicy);

		group.MapGet("/", GetMealPlan);
		group.MapPut("/slots", UpdateDaySlot);
		group.MapPost("/copy-category", CopyCategory);
		group.MapDelete("/slots/{itemIndex:int}", RemoveSlotItem);

		return app;
	}

	private static string? GetUserId(HttpContext httpContext) =>
		httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
		?? httpContext.User.FindFirst("sub")?.Value;

	private static Dictionary<string, string[]> ValidateUpdateDaySlotRequest(UpdateDaySlotRequest request)
	{
		var errors = new Dictionary<string, List<string>>();

		var rootResults = new List<ValidationResult>();
		Validator.TryValidateObject(request, new ValidationContext(request), rootResults, true);
		foreach (var result in rootResults)
		{
			var member = result.MemberNames.FirstOrDefault() ?? "request";
			if (!errors.TryGetValue(member, out var values))
			{
				values = [];
				errors[member] = values;
			}
			values.Add(result.ErrorMessage ?? "Invalid value");
		}

		for (var index = 0; index < request.Items.Count; index++)
		{
			var item = request.Items[index];
			var itemResults = new List<ValidationResult>();
			Validator.TryValidateObject(item, new ValidationContext(item), itemResults, true);
			foreach (var result in itemResults)
			{
				var member = result.MemberNames.FirstOrDefault() ?? "request";
				var key = $"Items[{index}].{member}";
				if (!errors.TryGetValue(key, out var values))
				{
					values = [];
					errors[key] = values;
				}
				values.Add(result.ErrorMessage ?? "Invalid value");
			}
		}

		return errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Distinct().ToArray());
	}

	private static async Task<IResult> GetMealPlan(
		HttpContext httpContext,
		IQueryHandler<GetMealPlanQuery, MealPlan> handler,
		IFamilyContextResolver familyResolver,
		CancellationToken cancellationToken,
		string? weekStart = null)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var familyResult = await familyResolver.ResolveAsync(userId, cancellationToken);
		if (!familyResult.IsSuccess)
			return Results.Problem(familyResult.Error!.Message, statusCode: 500);

		var week = string.IsNullOrEmpty(weekStart)
			? DateOnly.FromDateTime(DateTime.UtcNow)
			: DateOnly.ParseExact(weekStart, "yyyy-MM-dd");

		var result = await handler.HandleAsync(
			new GetMealPlanQuery(familyResult.Value!.FamilyGroupId, week), cancellationToken);
		return result.Match(
			onSuccess: plan => Results.Ok(MealPlanResponse.FromDomain(plan)),
			onFailure: error => Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> UpdateDaySlot(
		HttpContext httpContext,
		UpdateDaySlotRequest request,
		ICommandHandler<UpdateDaySlotCommand, MealPlan> handler,
		IMealPlanRealtimeNotifier realtimeNotifier,
		IFamilyContextResolver familyResolver,
		CancellationToken cancellationToken,
		string weekStart,
		string day,
		string category)
	{
		var validationErrors = ValidateUpdateDaySlotRequest(request);
		if (validationErrors.Count > 0)
			return Results.ValidationProblem(validationErrors);

		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var familyResult = await familyResolver.ResolveAsync(userId, cancellationToken);
		if (!familyResult.IsSuccess)
			return Results.Problem(familyResult.Error!.Message, statusCode: 500);
		var familyGroupId = familyResult.Value!.FamilyGroupId;

		if (!Enum.TryParse<DayOfWeek>(day, true, out var dayOfWeek))
			return Results.BadRequest($"Invalid day: {day}");

		if (!Enum.TryParse<MealCategory>(category, true, out var mealCategory))
			return Results.BadRequest($"Invalid category: {category}");

		var command = new UpdateDaySlotCommand(
			familyGroupId,
			DateOnly.ParseExact(weekStart, "yyyy-MM-dd"),
			dayOfWeek,
			mealCategory,
			request.Items.Select(i => i.ToDomain()).ToList());

		var result = await handler.HandleAsync(command, cancellationToken);
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

		var plan = result.Value!;
		await realtimeNotifier.PublishMealPlanUpdatedAsync(
			familyGroupId: familyGroupId,
			weekStart: plan.WeekStart,
			updatedPlan: plan,
			changedByUserId: userId,
			eventType: MealPlanRealtimeEventType.DaySlotUpdated,
			cancellationToken: cancellationToken);

		return Results.Ok(MealPlanResponse.FromDomain(plan));
	}

	private static async Task<IResult> CopyCategory(
		HttpContext httpContext,
		CopyCategoryRequest request,
		ICommandHandler<CopyCategoryCommand, MealPlan> handler,
		IMealPlanRealtimeNotifier realtimeNotifier,
		IFamilyContextResolver familyResolver,
		CancellationToken cancellationToken,
		string weekStart)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var familyResult = await familyResolver.ResolveAsync(userId, cancellationToken);
		if (!familyResult.IsSuccess)
			return Results.Problem(familyResult.Error!.Message, statusCode: 500);
		var familyGroupId = familyResult.Value!.FamilyGroupId;

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
			familyGroupId,
			DateOnly.ParseExact(weekStart, "yyyy-MM-dd"),
			sourceDay,
			category,
			targetDays);

		var result = await handler.HandleAsync(command, cancellationToken);
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

		var plan = result.Value!;
		await realtimeNotifier.PublishMealPlanUpdatedAsync(
			familyGroupId: familyGroupId,
			weekStart: plan.WeekStart,
			updatedPlan: plan,
			changedByUserId: userId,
			eventType: MealPlanRealtimeEventType.CategoryCopied,
			cancellationToken: cancellationToken);

		return Results.Ok(MealPlanResponse.FromDomain(plan));
	}

	private static async Task<IResult> RemoveSlotItem(
		int itemIndex,
		HttpContext httpContext,
		ICommandHandler<RemoveSlotItemCommand, MealPlan> handler,
		IMealPlanRealtimeNotifier realtimeNotifier,
		IFamilyContextResolver familyResolver,
		CancellationToken cancellationToken,
		string weekStart,
		string day,
		string category)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var familyResult = await familyResolver.ResolveAsync(userId, cancellationToken);
		if (!familyResult.IsSuccess)
			return Results.Problem(familyResult.Error!.Message, statusCode: 500);
		var familyGroupId = familyResult.Value!.FamilyGroupId;

		if (!Enum.TryParse<DayOfWeek>(day, true, out var dayOfWeek))
			return Results.BadRequest($"Invalid day: {day}");

		if (!Enum.TryParse<MealCategory>(category, true, out var mealCategory))
			return Results.BadRequest($"Invalid category: {category}");

		var command = new RemoveSlotItemCommand(
			familyGroupId,
			DateOnly.ParseExact(weekStart, "yyyy-MM-dd"),
			dayOfWeek,
			mealCategory,
			itemIndex);

		var result = await handler.HandleAsync(command, cancellationToken);
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

		var plan = result.Value!;
		await realtimeNotifier.PublishMealPlanUpdatedAsync(
			familyGroupId: familyGroupId,
			weekStart: plan.WeekStart,
			updatedPlan: plan,
			changedByUserId: userId,
			eventType: MealPlanRealtimeEventType.SlotItemRemoved,
			cancellationToken: cancellationToken);

		return Results.Ok(MealPlanResponse.FromDomain(plan));
	}
}
