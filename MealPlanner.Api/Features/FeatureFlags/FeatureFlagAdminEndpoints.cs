using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using MealPlanner.Api.Features.Auth;
using MealPlanner.Api.Features.FeatureFlags.Commands;
using MealPlanner.Api.Features.FeatureFlags.Dtos;
using MealPlanner.Api.Features.FeatureFlags.Models;
using MealPlanner.Api.Features.FeatureFlags.Queries;
using MealPlanner.Api.Shared;
using OpenFeature.Model;

namespace MealPlanner.Api.Features.FeatureFlags;

/// <summary>
/// Admin-only endpoints for authoring feature flags. Changes are persisted to
/// the database and picked up by flagd on its next sync poll, so a flag can be
/// created, retuned, or removed without a redeploy.
/// </summary>
public static class FeatureFlagAdminEndpoints
{
	public static IEndpointRouteBuilder MapFeatureFlagAdminEndpoints(this IEndpointRouteBuilder app)
	{
		var group = app.MapGroup("/api/admin/feature-flags")
			.WithTags("Admin", "FeatureFlags")
			.RequireAuthorization(RbacAuthorization.RequireAdminRolePolicy);

		group.MapGet("/", GetAll);
		group.MapGet("/{key}", GetByKey);
		group.MapPost("/", Create);
		group.MapPut("/{key}", Update);
		group.MapPatch("/{key}", SetEnabled);
		group.MapDelete("/{key}", Delete);
		group.MapPost("/{key}/evaluate", Evaluate);

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

	private static async Task<IResult> GetByKey(
		string key,
		IQueryHandler<GetFeatureFlagByKeyQuery, FeatureFlag> handler,
		CancellationToken cancellationToken)
	{
		var result = await handler.HandleAsync(new GetFeatureFlagByKeyQuery(key), cancellationToken);
		return result.Match(
			onSuccess: flag => Results.Ok(FeatureFlagDto.FromDomain(flag)),
			onFailure: ToProblem);
	}

	private static async Task<IResult> Create(
		CreateFeatureFlagRequest request,
		ICommandHandler<CreateFeatureFlagCommand, FeatureFlag> handler,
		CancellationToken cancellationToken)
	{
		var errors = ValidateRequest(request);
		if (errors.Count > 0)
		{
			return Results.ValidationProblem(errors);
		}

		var result = await handler.HandleAsync(
			new CreateFeatureFlagCommand(
				request.Key,
				request.Enabled,
				request.ValueType,
				request.DisabledVariant,
				request.DefinitionJson,
				request.Description),
			cancellationToken);

		return result.Match(
			onSuccess: flag => Results.Created(
				$"/api/admin/feature-flags/{flag.Key}", FeatureFlagDto.FromDomain(flag)),
			onFailure: ToProblem);
	}

	private static async Task<IResult> Update(
		string key,
		UpdateFeatureFlagRequest request,
		ICommandHandler<UpdateFeatureFlagCommand, FeatureFlag> handler,
		CancellationToken cancellationToken)
	{
		var errors = ValidateRequest(request);
		if (errors.Count > 0)
		{
			return Results.ValidationProblem(errors);
		}

		var result = await handler.HandleAsync(
			new UpdateFeatureFlagCommand(
				key,
				request.Enabled,
				request.ValueType,
				request.DisabledVariant,
				request.DefinitionJson,
				request.Description),
			cancellationToken);

		return result.Match(
			onSuccess: flag => Results.Ok(FeatureFlagDto.FromDomain(flag)),
			onFailure: ToProblem);
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
			onFailure: ToProblem);
	}

	private static async Task<IResult> Delete(
		string key,
		ICommandHandler<DeleteFeatureFlagCommand, Unit> handler,
		CancellationToken cancellationToken)
	{
		var result = await handler.HandleAsync(new DeleteFeatureFlagCommand(key), cancellationToken);
		return result.Match(
			onSuccess: _ => Results.NoContent(),
			onFailure: ToProblem);
	}

	/// <summary>
	/// Dry-runs a flag against an admin-supplied evaluation context so targeting
	/// rules can be checked without impersonating a user. This resolves through
	/// live flagd, so it reflects the document flagd last synced rather than any
	/// unsaved edits — the admin UI says as much.
	/// </summary>
	private static async Task<IResult> Evaluate(
		string key,
		EvaluateFeatureFlagRequest request,
		IQueryHandler<GetFeatureFlagByKeyQuery, FeatureFlag> flagHandler,
		IFeatureFlagClient featureFlags,
		ILoggerFactory loggerFactory,
		CancellationToken cancellationToken)
	{
		var flagResult = await flagHandler.HandleAsync(
			new GetFeatureFlagByKeyQuery(key), cancellationToken);

		if (!flagResult.IsSuccess)
		{
			return ToProblem(flagResult.Error!);
		}

		var flag = flagResult.Value!;
		var context = FeatureFlagContext.From(request.TargetingKey, request.Email, request.Role);

		try
		{
			var valueJson = await ResolveAsJsonAsync(featureFlags, flag, context, cancellationToken);

			return Results.Ok(new EvaluateFeatureFlagResponse
			{
				Key = flag.Key,
				ValueType = flag.ValueType,
				ValueJson = valueJson
			});
		}
		catch (Exception ex)
		{
			// A provider exception can carry internal infrastructure detail (the
			// flagd private host and port, for instance), so it goes to the logs
			// rather than back over the wire — matching how the CQRS handlers keep
			// the exception inside Error and return a curated message.
			loggerFactory
				.CreateLogger(typeof(FeatureFlagAdminEndpoints))
				.LogError(ex, "Failed to evaluate feature flag {FlagKey} through flagd.", flag.Key);

			return Results.Problem(
				"Failed to evaluate the feature flag through flagd. See the API logs for details.",
				statusCode: 502);
		}
	}

	/// <summary>
	/// Resolves a flag using the read method matching its value type and renders
	/// the result as JSON, so every value type travels back through one field.
	/// The defaults passed here are the "flagd had nothing for us" sentinels.
	/// </summary>
	private static async Task<string> ResolveAsJsonAsync(
		IFeatureFlagClient featureFlags,
		FeatureFlag flag,
		FeatureFlagContext context,
		CancellationToken cancellationToken)
	{
		switch (flag.ValueType)
		{
			case FeatureFlagValueTypes.String:
				var stringValue = await featureFlags.GetStringValueAsync(
					flag.Key, string.Empty, context, cancellationToken);
				return JsonSerializer.Serialize(stringValue);

			case FeatureFlagValueTypes.Number:
				var numberValue = await featureFlags.GetDoubleValueAsync(
					flag.Key, 0d, context, cancellationToken);
				return JsonSerializer.Serialize(numberValue);

			case FeatureFlagValueTypes.Object:
				var objectValue = await featureFlags.GetObjectValueAsync(
					flag.Key, new Value(), context, cancellationToken);
				return ToJsonNode(objectValue)?.ToJsonString() ?? "null";

			default:
				var booleanValue = await featureFlags.GetBooleanValueAsync(
					flag.Key, false, context, cancellationToken);
				return JsonSerializer.Serialize(booleanValue);
		}
	}

	/// <summary>
	/// Converts an OpenFeature <see cref="Value"/> into a JSON node. Serializing
	/// the <see cref="Value"/> wrapper directly would emit its internal shape
	/// rather than the flag's data, so the tree is walked explicitly.
	/// </summary>
	internal static JsonNode? ToJsonNode(Value value)
	{
		if (value.IsNull)
		{
			return null;
		}

		if (value.IsBoolean)
		{
			return JsonValue.Create(value.AsBoolean);
		}

		if (value.IsNumber)
		{
			return JsonValue.Create(value.AsDouble);
		}

		if (value.IsString)
		{
			return JsonValue.Create(value.AsString);
		}

		if (value.IsDateTime)
		{
			return JsonValue.Create(value.AsDateTime);
		}

		if (value.IsList)
		{
			var array = new JsonArray();
			foreach (var item in value.AsList!)
			{
				array.Add(ToJsonNode(item));
			}

			return array;
		}

		if (value.IsStructure)
		{
			var structure = new JsonObject();
			foreach (var (key, item) in value.AsStructure!.AsDictionary())
			{
				structure[key] = ToJsonNode(item);
			}

			return structure;
		}

		return null;
	}

	private static IResult ToProblem(Error error) => error.Code switch
	{
		ErrorCodes.NotFound => Results.NotFound(error.Message),
		ErrorCodes.Conflict => Results.Conflict(error.Message),
		ErrorCodes.ValidationFailed => Results.BadRequest(error.Message),
		_ => Results.Problem(error.Message, statusCode: 500)
	};

	/// <summary>
	/// Runs DataAnnotations over a request body, matching the convention used by
	/// the other admin endpoint groups.
	/// </summary>
	private static Dictionary<string, string[]> ValidateRequest(object request)
	{
		var validationResults = new List<ValidationResult>();
		Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true);
		return validationResults
			.GroupBy(
				r => r.MemberNames.FirstOrDefault() ?? "request",
				r => r.ErrorMessage ?? "Invalid value")
			.ToDictionary(g => g.Key, g => g.Distinct().ToArray());
	}
}
