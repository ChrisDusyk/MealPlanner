using System.Security.Claims;
using MealPlanner.Api.Features.Auth;
using MealPlanner.Api.Features.Families.Commands;
using MealPlanner.Api.Features.Families.Dtos;
using MealPlanner.Api.Features.Families.Models;
using MealPlanner.Api.Features.Families.Queries;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Families;

/// <summary>
/// Maps minimal API endpoints for family group management.
/// </summary>
public static class FamilyEndpoints
{
	public static IEndpointRouteBuilder MapFamilyEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/families")
			.WithTags("Families")
			.RequireAuthorization(RbacAuthorization.RequireUserRolePolicy);

		group.MapGet("/mine", GetMyFamily);
		group.MapPut("/mine", RenameFamily);
		group.MapDelete("/mine", DeleteFamily);
		group.MapPost("/mine/leave", LeaveFamily);
		group.MapPost("/mine/transfer-ownership", TransferOwnership);
		group.MapDelete("/mine/members/{memberUserId}", RemoveMember);
		group.MapPost("/mine/invitations", InviteToFamily);
		group.MapDelete("/mine/invitations/{invitationId}", CancelInvitation);
		group.MapGet("/invitations/incoming", GetIncomingInvitations);
		group.MapPost("/invitations/{invitationId}/accept", AcceptInvitation);
		group.MapPost("/invitations/{invitationId}/decline", DeclineInvitation);

		return app;
	}

	private static string? GetUserId(HttpContext httpContext) =>
		httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
		?? httpContext.User.FindFirst("sub")?.Value;

	private static IResult ToErrorResult(Error error) => error.Code switch
	{
		ErrorCodes.NotFound => Results.NotFound(error.Message),
		ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
		ErrorCodes.Unauthorized => Results.Forbid(),
		_ => Results.Problem(error.Message, statusCode: 500)
	};

	private static async Task<IResult> GetMyFamily(
		HttpContext httpContext,
		IQueryHandler<GetMyFamilyQuery, MyFamilyResult> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(new GetMyFamilyQuery(userId), cancellationToken);
		return result.Match(
			onSuccess: family => Results.Ok(MyFamilyResponse.FromDomain(family)),
			onFailure: ToErrorResult);
	}

	private static async Task<IResult> RenameFamily(
		RenameFamilyRequest request,
		HttpContext httpContext,
		ICommandHandler<RenameFamilyCommand, MyFamilyResult> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new RenameFamilyCommand(userId, request.Name), cancellationToken);
		return result.Match(
			onSuccess: family => Results.Ok(MyFamilyResponse.FromDomain(family)),
			onFailure: ToErrorResult);
	}

	private static async Task<IResult> DeleteFamily(
		HttpContext httpContext,
		ICommandHandler<DeleteFamilyCommand, MyFamilyResult> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(new DeleteFamilyCommand(userId), cancellationToken);
		return result.Match(
			onSuccess: family => Results.Ok(MyFamilyResponse.FromDomain(family)),
			onFailure: ToErrorResult);
	}

	private static async Task<IResult> LeaveFamily(
		HttpContext httpContext,
		ICommandHandler<LeaveFamilyCommand, MyFamilyResult> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(new LeaveFamilyCommand(userId), cancellationToken);
		return result.Match(
			onSuccess: family => Results.Ok(MyFamilyResponse.FromDomain(family)),
			onFailure: ToErrorResult);
	}

	private static async Task<IResult> TransferOwnership(
		TransferFamilyOwnershipRequest request,
		HttpContext httpContext,
		ICommandHandler<TransferFamilyOwnershipCommand, MyFamilyResult> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new TransferFamilyOwnershipCommand(userId, request.NewOwnerUserId), cancellationToken);
		return result.Match(
			onSuccess: family => Results.Ok(MyFamilyResponse.FromDomain(family)),
			onFailure: ToErrorResult);
	}

	private static async Task<IResult> RemoveMember(
		string memberUserId,
		HttpContext httpContext,
		ICommandHandler<RemoveFamilyMemberCommand, MyFamilyResult> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new RemoveFamilyMemberCommand(userId, memberUserId), cancellationToken);
		return result.Match(
			onSuccess: family => Results.Ok(MyFamilyResponse.FromDomain(family)),
			onFailure: ToErrorResult);
	}

	private static async Task<IResult> InviteToFamily(
		InviteToFamilyRequest request,
		HttpContext httpContext,
		ICommandHandler<InviteToFamilyCommand, FamilyInvitation> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new InviteToFamilyCommand(userId, request.Email), cancellationToken);
		return result.Match(
			onSuccess: invitation => Results.Created(
				$"/api/families/mine/invitations/{invitation.Id}",
				FamilyInvitationResponse.FromDomain(invitation)),
			onFailure: ToErrorResult);
	}

	private static async Task<IResult> CancelInvitation(
		string invitationId,
		HttpContext httpContext,
		ICommandHandler<CancelFamilyInvitationCommand, Unit> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new CancelFamilyInvitationCommand(userId, invitationId), cancellationToken);
		return result.Match(
			onSuccess: _ => Results.NoContent(),
			onFailure: ToErrorResult);
	}

	private static async Task<IResult> GetIncomingInvitations(
		HttpContext httpContext,
		IQueryHandler<GetIncomingFamilyInvitationsQuery, List<FamilyInvitation>> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new GetIncomingFamilyInvitationsQuery(userId), cancellationToken);
		return result.Match(
			onSuccess: invitations => Results.Ok(
				invitations.Select(FamilyInvitationResponse.FromDomain).ToList()),
			onFailure: ToErrorResult);
	}

	private static async Task<IResult> AcceptInvitation(
		string invitationId,
		HttpContext httpContext,
		ICommandHandler<AcceptFamilyInvitationCommand, MyFamilyResult> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new AcceptFamilyInvitationCommand(userId, invitationId), cancellationToken);
		return result.Match(
			onSuccess: family => Results.Ok(MyFamilyResponse.FromDomain(family)),
			onFailure: ToErrorResult);
	}

	private static async Task<IResult> DeclineInvitation(
		string invitationId,
		HttpContext httpContext,
		ICommandHandler<DeclineFamilyInvitationCommand, Unit> handler,
		CancellationToken cancellationToken)
	{
		var userId = GetUserId(httpContext);
		if (userId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new DeclineFamilyInvitationCommand(userId, invitationId), cancellationToken);
		return result.Match(
			onSuccess: _ => Results.NoContent(),
			onFailure: ToErrorResult);
	}
}
