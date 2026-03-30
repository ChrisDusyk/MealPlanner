using System.Security.Claims;
using MealPlanner.Api.Features.Auth;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Commands;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Dtos;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Queries;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep;

public static class GoogleKeepEndpoints
{
	public static IEndpointRouteBuilder MapGoogleKeepEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/integrations/google-keep")
			.WithTags("Google Keep")
			.RequireAuthorization(RbacAuthorization.RequireUserRolePolicy);

		group.MapPost("/connect/start", StartConnection);
		group.MapPost("/connect/complete", CompleteConnection);
		group.MapGet("/status", GetStatus);
		group.MapDelete("/connect", DisconnectConnection);

		var exportGroup = app.MapGroup("/api/grocery-lists/export/google-keep")
			.WithTags("Google Keep")
			.RequireAuthorization(RbacAuthorization.RequireUserRolePolicy);
		exportGroup.MapPost("/", ExportGroceryList);

		return app;
	}

	private static string? GetUserId(HttpContext httpContext) =>
		httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
		?? httpContext.User.FindFirst("sub")?.Value;

	private static async Task<IResult> StartConnection(
		StartGoogleKeepConnectionRequest request,
		HttpContext httpContext,
		ICommandHandler<StartGoogleKeepConnectionCommand, StartGoogleKeepConnectionResult> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new StartGoogleKeepConnectionCommand(userId, request.RedirectBaseUri),
			cancellationToken);
		return result.Match(
			onSuccess: start => Results.Ok(StartGoogleKeepConnectionResponse.FromDomain(start)),
			onFailure: error => error.Code == ErrorCodes.ValidationFailed
				? Results.BadRequest(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> CompleteConnection(
		CompleteGoogleKeepConnectionRequest request,
		HttpContext httpContext,
		ICommandHandler<CompleteGoogleKeepConnectionCommand, GoogleKeepConnectionStatus> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new CompleteGoogleKeepConnectionCommand(userId, request.Code, request.State),
			cancellationToken);
		return result.Match(
			onSuccess: status => Results.Ok(GoogleKeepConnectionStatusResponse.FromDomain(status)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				ErrorCodes.Unauthorized => Results.Unauthorized(),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> GetStatus(
		HttpContext httpContext,
		IQueryHandler<GetGoogleKeepConnectionStatusQuery, GoogleKeepConnectionStatus> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(new GetGoogleKeepConnectionStatusQuery(userId), cancellationToken);
		return result.Match(
			onSuccess: status => Results.Ok(GoogleKeepConnectionStatusResponse.FromDomain(status)),
			onFailure: error => error.Code == ErrorCodes.ValidationFailed
				? Results.BadRequest(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> DisconnectConnection(
		HttpContext httpContext,
		ICommandHandler<DisconnectGoogleKeepConnectionCommand, Unit> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(new DisconnectGoogleKeepConnectionCommand(userId), cancellationToken);
		return result.Match(
			onSuccess: _ => Results.NoContent(),
			onFailure: error => error.Code == ErrorCodes.ValidationFailed
				? Results.BadRequest(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> ExportGroceryList(
		ExportGroceryListToGoogleKeepRequest request,
		HttpContext httpContext,
		ICommandHandler<ExportGroceryListToGoogleKeepCommand, GoogleKeepExportResult> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		if (!DateOnly.TryParseExact(request.WeekStart, "yyyy-MM-dd", out var weekStart))
			return Results.BadRequest("weekStart must be a valid date in yyyy-MM-dd format.");

		var result = await handler.HandleAsync(
			new ExportGroceryListToGoogleKeepCommand(userId, weekStart, request.ForceNewNote),
			cancellationToken);

		return result.Match(
			onSuccess: export => Results.Ok(GoogleKeepExportResponse.FromDomain(export)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}
}
