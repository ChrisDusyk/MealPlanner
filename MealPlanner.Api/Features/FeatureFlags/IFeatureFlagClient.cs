using OpenFeature;
using OpenFeature.Model;

namespace MealPlanner.Api.Features.FeatureFlags;

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
		string? targetingKey = null,
		CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation backed by the OpenFeature client, whose provider is
/// the flagd RPC provider configured in <c>Program.cs</c>.
/// </summary>
public sealed class OpenFeatureFlagClient : IFeatureFlagClient
{
	public async Task<bool> GetBooleanValueAsync(
		string flagKey,
		bool defaultValue,
		string? targetingKey = null,
		CancellationToken cancellationToken = default)
	{
		var client = OpenFeature.Api.Instance.GetClient();

		EvaluationContext? context = null;
		if (!string.IsNullOrWhiteSpace(targetingKey))
		{
			context = EvaluationContext.Builder()
				.SetTargetingKey(targetingKey)
				.Build();
		}

		return await client.GetBooleanValueAsync(
			flagKey,
			defaultValue,
			context,
			cancellationToken: cancellationToken);
	}
}
