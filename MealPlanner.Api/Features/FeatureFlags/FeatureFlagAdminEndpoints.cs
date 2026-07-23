using MealPlanner.Api.Features.Auth;
using MealPlanner.Api.Features.FeatureFlags.Commands;
using MealPlanner.Api.Features.FeatureFlags.Dtos;
using MealPlanner.Api.Features.FeatureFlags.Models;
using MealPlanner.Api.Features.FeatureFlags.Queries;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.FeatureFlags;

/// <summary>
/// Admin-only endpoints for viewing and toggling feature flags. Changes are
/// persisted to the database and picked up by flagd on its next sync poll.
/// </summary>
public static class FeatureFlagAdminEndpoints
{
	public static IEndpointRouteBuilder MapFeatureFlagAdminEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/admin/feature-flags")
			.WithTags("Admin", "FeatureFlags")
			.RequireAuthorization(RbacAuthorization.RequireAdminRolePolicy);

		group.MapGet("/", GetAll);
		group.MapPatch("/{key}", SetEnabled);

		return app;
	}

	private static async Task<IResult> GetAll(
		IQueryHandler<GetFeatureFlagsQuery, IReadOnlyList<FeatureFlag>> handler,
		CancellationToken cancellationToken)
	{
		var result = await handler.HandleAsync(new GetFeatureFlagsQuery(), cancellationToken);
		return result.Match(
			onSuccess: flags => Results.Ok(flags.Select(FeatureFlagDto.FromDomain).ToList()),
			onFailure: error => Results.Problem(error.Message, statusCode: 500));
	}

	private static async Task<IResult> SetEnabled(
		string key,
		SetFeatureFlagEnabledRequest request,
		ICommandHandler<SetFeatureFlagEnabledCommand, FeatureFlag> handler,
		CancellationToken cancellationToken)
	{
		var result = await handler.HandleAsync(
			new SetFeatureFlagEnabledCommand(key, request.Enabled), cancellationToken);
		return result.Match(
			onSuccess: flag => Results.Ok(FeatureFlagDto.FromDomain(flag)),
			onFailure: error => error.Code switch
			{
				ErrorCodes.NotFound => Results.NotFound(error.Message),
				ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
				_ => Results.Problem(error.Message, statusCode: 500)
			});
	}
}
