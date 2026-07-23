namespace MealPlanner.Api.Features.FeatureFlags;

/// <summary>
/// Configuration for the OpenFeature/flagd integration.
/// </summary>
public class FeatureFlagsOptions
{
	public const string SectionName = "FeatureFlags";

	/// <summary>
	/// Host of the flagd gRPC evaluation endpoint. In development this is
	/// supplied by the Aspire flagd reference; in production it is the flagd
	/// service's private-network address.
	/// </summary>
	public string Host { get; set; } = "localhost";

	/// <summary>
	/// Port of the flagd gRPC evaluation endpoint (flagd's default is 8013).
	/// </summary>
	public int Port { get; set; } = 8013;

	/// <summary>
	/// Optional shared secret required on the internal sync endpoint. When set,
	/// flagd must send it in the <c>X-Flags-Sync-Token</c> header. When empty,
	/// the endpoint relies on private-network isolation only.
	/// </summary>
	public string SyncToken { get; set; } = string.Empty;

	/// <summary>
	/// Overrides <see cref="Host"/>/<see cref="Port"/> from the Aspire-injected
	/// flagd connection string (<c>ConnectionStrings:flagd</c> =
	/// <c>http://{host}:{port}</c>) when present, so development uses service
	/// discovery while production falls back to the bound host/port.
	/// </summary>
	public static void ApplyFlagdConnectionString(FeatureFlagsOptions options, IConfiguration configuration)
	{
		var connectionString = configuration.GetConnectionString("flagd");
		if (!string.IsNullOrWhiteSpace(connectionString)
			&& Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
		{
			options.Host = uri.Host;
			options.Port = uri.Port;
		}
	}
}
