namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Options;

public sealed class GoogleIntegrationsOptions
{
	public const string SectionName = "GoogleIntegrations";
	public const string OAuthHttpClientName = "GoogleOAuth";
	public const string KeepHttpClientName = "GoogleKeep";

	public string ClientId { get; set; } = string.Empty;

	public string ClientSecret { get; set; } = string.Empty;

	public string RedirectUri { get; set; } = string.Empty;

	public string TokenProtectionPurpose { get; set; } = string.Empty;

	public string DataProtectionApplicationName { get; set; } = "MealPlanner.Api";

	public string DataProtectionKeyRingPath { get; set; } = string.Empty;

	public List<string> KeepScopes { get; set; } = [];

	public string FallbackProviderWhenKeepUnavailable { get; set; } = string.Empty;
}
