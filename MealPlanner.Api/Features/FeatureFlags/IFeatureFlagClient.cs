using MealPlanner.Api.Shared;
using OpenFeature;
using OpenFeature.Model;

namespace MealPlanner.Api.Features.FeatureFlags;

/// <summary>
/// Attributes handed to flagd when resolving a flag. Targeting rules authored in
/// the admin UI can only match on what appears here, so widening this record is
/// what unlocks new targeting attributes.
/// </summary>
public record FeatureFlagContext(
	Option<string> TargetingKey,
	Option<string> Email,
	Option<string> Role)
{
	public static readonly FeatureFlagContext Empty = new(
		Option<string>.None(), Option<string>.None(), Option<string>.None());

	/// <summary>
	/// Builds a context from the nullable values that arrive from claims and
	/// request bodies.
	/// </summary>
	public static FeatureFlagContext From(string? targetingKey, string? email = null, string? role = null) =>
		new(Option<string>.From(targetingKey), Option<string>.From(email), Option<string>.From(role));

	/// <summary>
	/// Converts to an OpenFeature context, or null when nothing is set — flagd
	/// treats an empty context and no context alike, and passing null keeps the
	/// existing behaviour for anonymous evaluations.
	/// </summary>
	internal EvaluationContext? ToEvaluationContext()
	{
		if (!TargetingKey.HasValue && !Email.HasValue && !Role.HasValue)
		{
			return null;
		}

		var builder = EvaluationContext.Builder();

		if (TargetingKey.HasValue)
		{
			builder.SetTargetingKey(TargetingKey.Value);
		}

		if (Email.HasValue)
		{
			builder.Set("email", Email.Value);
		}

		if (Role.HasValue)
		{
			builder.Set("role", Role.Value);
		}

		return builder.Build();
	}
}

/// <summary>
/// Thin abstraction over the OpenFeature client so handlers and endpoints can
/// resolve flags without taking a hard dependency on the static OpenFeature
/// API, which keeps them unit-testable with a mocked client.
/// </summary>
public interface IFeatureFlagClient
{
	Task<bool> GetBooleanValueAsync(
		string flagKey,
		bool defaultValue,
		FeatureFlagContext? context = null,
		CancellationToken cancellationToken = default);

	Task<string> GetStringValueAsync(
		string flagKey,
		string defaultValue,
		FeatureFlagContext? context = null,
		CancellationToken cancellationToken = default);

	Task<double> GetDoubleValueAsync(
		string flagKey,
		double defaultValue,
		FeatureFlagContext? context = null,
		CancellationToken cancellationToken = default);

	Task<Value> GetObjectValueAsync(
		string flagKey,
		Value defaultValue,
		FeatureFlagContext? context = null,
		CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation backed by the OpenFeature client, whose provider is
/// the flagd RPC provider configured in <c>Program.cs</c>.
/// </summary>
public sealed class OpenFeatureFlagClient : IFeatureFlagClient
{
	public Task<bool> GetBooleanValueAsync(
		string flagKey,
		bool defaultValue,
		FeatureFlagContext? context = null,
		CancellationToken cancellationToken = default) =>
		OpenFeature.Api.Instance.GetClient().GetBooleanValueAsync(
			flagKey, defaultValue, context?.ToEvaluationContext(), cancellationToken: cancellationToken);

	public Task<string> GetStringValueAsync(
		string flagKey,
		string defaultValue,
		FeatureFlagContext? context = null,
		CancellationToken cancellationToken = default) =>
		OpenFeature.Api.Instance.GetClient().GetStringValueAsync(
			flagKey, defaultValue, context?.ToEvaluationContext(), cancellationToken: cancellationToken);

	public Task<double> GetDoubleValueAsync(
		string flagKey,
		double defaultValue,
		FeatureFlagContext? context = null,
		CancellationToken cancellationToken = default) =>
		OpenFeature.Api.Instance.GetClient().GetDoubleValueAsync(
			flagKey, defaultValue, context?.ToEvaluationContext(), cancellationToken: cancellationToken);

	public Task<Value> GetObjectValueAsync(
		string flagKey,
		Value defaultValue,
		FeatureFlagContext? context = null,
		CancellationToken cancellationToken = default) =>
		OpenFeature.Api.Instance.GetClient().GetObjectValueAsync(
			flagKey, defaultValue, context?.ToEvaluationContext(), cancellationToken: cancellationToken);
}
