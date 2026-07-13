using System.Net;
using System.Net.Sockets;

namespace MealPlanner.Api.Features.Recipes.Import;

/// <summary>
/// SSRF guards for the recipe page-fetch HTTP client. The pre-flight check in
/// <see cref="RecipePageTextExtractor"/> gives users a friendly error, but on its own
/// it can be bypassed by a redirect to an internal host or by DNS rebinding between
/// validation and fetch. The handler produced here closes that gap by re-resolving
/// and validating the destination address at connect time, for every connection —
/// including each redirect hop.
/// </summary>
internal static class SsrfProtection
{
	private const int MaxRedirects = 5;

	internal static SocketsHttpHandler CreatePageFetchHandler() => new()
	{
		MaxAutomaticRedirections = MaxRedirects,
		ConnectCallback = static async (context, cancellationToken) =>
		{
			var host = context.DnsEndPoint.Host;

			IPAddress[] addresses;
			if (IPAddress.TryParse(host, out var literal))
			{
				addresses = [literal];
			}
			else
			{
				addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
			}

			if (addresses.Length == 0 || addresses.Any(IsPrivateOrLocalAddress))
			{
				throw new HttpRequestException(
					$"Connection to '{host}' was blocked because it resolves to a private or local address.");
			}

			var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
			try
			{
				await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
				return new NetworkStream(socket, ownsSocket: true);
			}
			catch
			{
				socket.Dispose();
				throw;
			}
		}
	};

	internal static bool IsPrivateOrLocalAddress(IPAddress address)
	{
		if (IPAddress.IsLoopback(address))
			return true;

		if (address.IsIPv4MappedToIPv6)
			address = address.MapToIPv4();

		if (address.AddressFamily == AddressFamily.InterNetwork)
		{
			var bytes = address.GetAddressBytes();
			return bytes[0] switch
			{
				0 => true,
				10 => true,
				127 => true,
				169 when bytes[1] == 254 => true,
				172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
				192 when bytes[1] == 168 => true,
				_ => false
			};
		}

		if (address.AddressFamily == AddressFamily.InterNetworkV6)
		{
			var bytes = address.GetAddressBytes();
			// fc00::/7 — unique local addresses
			var isUniqueLocal = (bytes[0] & 0xFE) == 0xFC;

			return address.IsIPv6LinkLocal
			       || address.IsIPv6Multicast
			       || address.IsIPv6SiteLocal
			       || isUniqueLocal
			       || address.Equals(IPAddress.IPv6Loopback)
			       || address.Equals(IPAddress.IPv6Any);
		}

		return false;
	}
}
