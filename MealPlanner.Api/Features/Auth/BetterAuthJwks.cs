using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace MealPlanner.Api.Features.Auth;

/// <summary>
/// Fetches Better Auth's JWKS document from <c>{authority}/api/auth/jwks</c> and
/// wraps it in an <see cref="OpenIdConnectConfiguration"/> so the standard
/// <c>JwtBearerOptions.ConfigurationManager</c> pipeline can resolve signing keys.
///
/// Better Auth's JWT plugin only publishes JWKS (not full OIDC discovery) so this
/// retriever synthesises the minimal shape .NET's token handler requires.
/// </summary>
internal sealed class BetterAuthJwksConfigurationRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
{
	public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
		string address,
		IDocumentRetriever retriever,
		CancellationToken cancel)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(address);
		ArgumentNullException.ThrowIfNull(retriever);

		var jwksJson = await retriever.GetDocumentAsync(address, cancel).ConfigureAwait(false);
		var jwks = new JsonWebKeySet(jwksJson);

		var configuration = new OpenIdConnectConfiguration
		{
			JsonWebKeySet = jwks
		};

		foreach (var key in jwks.GetSigningKeys())
		{
			configuration.SigningKeys.Add(key);
		}

		return configuration;
	}
}
