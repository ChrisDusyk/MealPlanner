using System.Security.Claims;
using MealPlanner.Api.Features.Users.Commands;
using MealPlanner.Api.Features.Users.Dtos;
using MealPlanner.Api.Features.Users.Models;
using MealPlanner.Api.Features.Users.Queries;
using MealPlanner.Api.Features.Users.Realtime;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Users;

/// <summary>
/// Maps minimal API endpoints for user management.
/// </summary>
public static class UserEndpoints
{
	public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/users")
			.WithTags("Users")
			.RequireAuthorization();

		group.MapPost("/sync", SyncUser);
		group.MapGet("/me", GetCurrentUser);
		group.MapPut("/me", UpdateCurrentUser);
		group.MapGet("/search", SearchUserByEmail);
		group.MapGet("/friends", GetFriends);
		group.MapGet("/friends/requests/incoming", GetIncomingFriendRequests);
		group.MapGet("/friends/requests/outgoing", GetOutgoingFriendRequests);
		group.MapPost("/friends/requests", SendFriendRequest);
		group.MapPost("/friends/requests/{requestId}/accept", AcceptFriendRequest);
		group.MapPost("/friends/requests/{requestId}/reject", RejectFriendRequest);
		group.MapDelete("/friends/{friendUserId}", RemoveFriend);

		return app;
	}

	private static string? GetAuth0UserId(HttpContext httpContext) =>
		httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
		?? httpContext.User.FindFirst("sub")?.Value;

	private static string? GetClaimValue(HttpContext httpContext, params string[] claimTypes)
	{
		foreach (var claimType in claimTypes)
		{
			var value = httpContext.User.FindFirst(claimType)?.Value;
			if (!string.IsNullOrWhiteSpace(value))
			{
				return value;
			}
		}

		return null;
	}

	private static async Task<IResult> SyncUser(
		SyncUserRequest request,
		HttpContext httpContext,
		ICommandHandler<UpsertUserFromAuthCommand, User> handler,
		CancellationToken cancellationToken)
	{
		var auth0UserId = GetAuth0UserId(httpContext);
		if (auth0UserId is null)
			return Results.Unauthorized();

		var claimEmail = GetClaimValue(httpContext, ClaimTypes.Email, "email");
		var resolvedEmail = string.IsNullOrWhiteSpace(request.Email)
			? claimEmail
			: request.Email;
		var resolvedName = !string.IsNullOrWhiteSpace(request.Name)
			? request.Name
			: GetClaimValue(httpContext, ClaimTypes.Name, "name", "nickname")
			  ?? resolvedEmail
			  ?? auth0UserId;

		var command = new UpsertUserFromAuthCommand(
			auth0UserId,
			resolvedName,
			Option<string>.From(string.IsNullOrWhiteSpace(resolvedEmail) ? null : resolvedEmail));

		var result = await handler.HandleAsync(command, cancellationToken);
		return result.Match(
			onSuccess: user => Results.Ok(UserResponse.FromDomain(user)),
			onFailure: error => error.Code == ErrorCodes.ValidationFailed
				? Results.BadRequest(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> GetCurrentUser(
		HttpContext httpContext,
		IQueryHandler<FindUserByAuth0IdQuery, User> handler,
		CancellationToken cancellationToken)
	{
		var auth0UserId = GetAuth0UserId(httpContext);
		if (auth0UserId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(new FindUserByAuth0IdQuery(auth0UserId), cancellationToken);
		return result.Match(
			onSuccess: user => Results.Ok(UserResponse.FromDomain(user)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> UpdateCurrentUser(
		UpdateCurrentUserRequest request,
		HttpContext httpContext,
		ICommandHandler<UpdateCurrentUserNameCommand, User> handler,
		CancellationToken cancellationToken)
	{
		var auth0UserId = GetAuth0UserId(httpContext);
		if (auth0UserId is null)
			return Results.Unauthorized();

		var command = new UpdateCurrentUserNameCommand(
			auth0UserId,
			request.Name?.Trim() ?? string.Empty);

		var result = await handler.HandleAsync(command, cancellationToken);
		return result.Match(
			onSuccess: user => Results.Ok(UserResponse.FromDomain(user)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> SearchUserByEmail(
		HttpContext httpContext,
		IQueryHandler<FindUserByEmailQuery, User> handler,
		CancellationToken cancellationToken,
		string email)
	{
		var auth0UserId = GetAuth0UserId(httpContext);
		if (auth0UserId is null)
			return Results.Unauthorized();

		if (string.IsNullOrWhiteSpace(email))
			return Results.BadRequest("Email query parameter is required.");

		var result = await handler.HandleAsync(new FindUserByEmailQuery(email), cancellationToken);
		return result.Match(
			onSuccess: user => Results.Ok(UserSummaryResponse.FromDomain(user)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}

	private static async Task<IResult> GetFriends(
		HttpContext httpContext,
		IQueryHandler<GetFriendsForUserQuery, IReadOnlyList<FriendSummary>> handler,
		CancellationToken cancellationToken)
	{
		var auth0UserId = GetAuth0UserId(httpContext);
		if (auth0UserId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(new GetFriendsForUserQuery(auth0UserId), cancellationToken);
		return result.Match(
			onSuccess: friends => Results.Ok(friends.Select(FriendSummaryResponse.FromDomain)),
			onFailure: error => error.Code == ErrorCodes.ValidationFailed
				? Results.BadRequest(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> GetIncomingFriendRequests(
		HttpContext httpContext,
		IQueryHandler<GetIncomingFriendRequestsQuery, IReadOnlyList<FriendRequestSummary>> handler,
		CancellationToken cancellationToken)
	{
		var auth0UserId = GetAuth0UserId(httpContext);
		if (auth0UserId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(new GetIncomingFriendRequestsQuery(auth0UserId), cancellationToken);
		return result.Match(
			onSuccess: requests => Results.Ok(requests.Select(FriendRequestSummaryResponse.FromDomain)),
			onFailure: error => error.Code == ErrorCodes.ValidationFailed
				? Results.BadRequest(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> GetOutgoingFriendRequests(
		HttpContext httpContext,
		IQueryHandler<GetOutgoingFriendRequestsQuery, IReadOnlyList<FriendRequestSummary>> handler,
		CancellationToken cancellationToken)
	{
		var auth0UserId = GetAuth0UserId(httpContext);
		if (auth0UserId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(new GetOutgoingFriendRequestsQuery(auth0UserId), cancellationToken);
		return result.Match(
			onSuccess: requests => Results.Ok(requests.Select(FriendRequestSummaryResponse.FromDomain)),
			onFailure: error => error.Code == ErrorCodes.ValidationFailed
				? Results.BadRequest(error.Message)
				: Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> SendFriendRequest(
		SendFriendRequestRequest request,
		HttpContext httpContext,
		ICommandHandler<SendFriendRequestByEmailCommand, SendFriendRequestResult> handler,
		IFriendsRealtimeNotifier realtimeNotifier,
		CancellationToken cancellationToken)
	{
		var auth0UserId = GetAuth0UserId(httpContext);
		if (auth0UserId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new SendFriendRequestByEmailCommand(auth0UserId, request.Email?.Trim() ?? string.Empty),
			cancellationToken);

		if (!result.IsSuccess)
		{
			var error = result.Error!;
			return error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			};
		}

		var sendResult = result.Value!;
		await realtimeNotifier.PublishFriendsUpdatedAsync(
			[auth0UserId, sendResult.RecipientUserId],
			auth0UserId,
			sendResult.Status == SendFriendRequestStatus.Accepted
				? FriendsRealtimeEventType.RequestAccepted
				: FriendsRealtimeEventType.RequestSent,
			cancellationToken);

		return Results.Ok(SendFriendRequestResponse.FromDomain(sendResult));
	}

	private static async Task<IResult> AcceptFriendRequest(
		string requestId,
		HttpContext httpContext,
		ICommandHandler<AcceptFriendRequestCommand, FriendRequestActionResult> handler,
		IFriendsRealtimeNotifier realtimeNotifier,
		CancellationToken cancellationToken)
	{
		var auth0UserId = GetAuth0UserId(httpContext);
		if (auth0UserId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new AcceptFriendRequestCommand(auth0UserId, requestId),
			cancellationToken);

		if (!result.IsSuccess)
		{
			var error = result.Error!;
			return error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			};
		}

		await realtimeNotifier.PublishFriendsUpdatedAsync(
			[auth0UserId, result.Value!.RequesterUserId],
			auth0UserId,
			FriendsRealtimeEventType.RequestAccepted,
			cancellationToken);

		return Results.Ok(new FriendRequestActionResponse(true));
	}

	private static async Task<IResult> RejectFriendRequest(
		string requestId,
		HttpContext httpContext,
		ICommandHandler<RejectFriendRequestCommand, FriendRequestActionResult> handler,
		IFriendsRealtimeNotifier realtimeNotifier,
		CancellationToken cancellationToken)
	{
		var auth0UserId = GetAuth0UserId(httpContext);
		if (auth0UserId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new RejectFriendRequestCommand(auth0UserId, requestId),
			cancellationToken);

		if (!result.IsSuccess)
		{
			var error = result.Error!;
			return error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			};
		}

		await realtimeNotifier.PublishFriendsUpdatedAsync(
			[auth0UserId, result.Value!.RequesterUserId],
			auth0UserId,
			FriendsRealtimeEventType.RequestRejected,
			cancellationToken);

		return Results.Ok(new FriendRequestActionResponse(true));
	}

	private static async Task<IResult> RemoveFriend(
		string friendUserId,
		HttpContext httpContext,
		ICommandHandler<RemoveFriendCommand, Unit> handler,
		IFriendsRealtimeNotifier realtimeNotifier,
		CancellationToken cancellationToken)
	{
		var auth0UserId = GetAuth0UserId(httpContext);
		if (auth0UserId is null)
			return Results.Unauthorized();

		var result = await handler.HandleAsync(
			new RemoveFriendCommand(auth0UserId, friendUserId),
			cancellationToken);

		if (!result.IsSuccess)
		{
			var error = result.Error!;
			return error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			};
		}

		await realtimeNotifier.PublishFriendsUpdatedAsync(
			[auth0UserId, friendUserId],
			auth0UserId,
			FriendsRealtimeEventType.FriendshipRemoved,
			cancellationToken);

		return Results.Ok(new FriendRequestActionResponse(true));
	}
}
