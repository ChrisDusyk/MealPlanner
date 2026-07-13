using System.Net;
using MealPlanner.Api.Features.Recipes.Import;

namespace MealPlanner.Api.Tests.Features.Recipes.Import;

public class SsrfProtectionTests
{
	[Theory]
	[InlineData("127.0.0.1")]
	[InlineData("127.8.9.10")]
	[InlineData("0.0.0.0")]
	[InlineData("10.0.0.5")]
	[InlineData("169.254.169.254")]
	[InlineData("172.16.0.1")]
	[InlineData("172.31.255.255")]
	[InlineData("192.168.1.1")]
	[InlineData("100.64.0.1")]
	[InlineData("100.127.255.255")]
	[InlineData("198.18.0.1")]
	[InlineData("198.19.255.255")]
	[InlineData("224.0.0.251")]
	[InlineData("240.0.0.1")]
	[InlineData("255.255.255.255")]
	[InlineData("::1")]
	[InlineData("::")]
	[InlineData("fe80::1")]
	[InlineData("fc00::1")]
	[InlineData("fd12:3456:789a::1")]
	[InlineData("ff02::1")]
	[InlineData("::ffff:192.168.0.1")]
	[InlineData("::ffff:10.0.0.1")]
	public void IsPrivateOrLocalAddress_ReturnsTrue_ForPrivateOrLocalAddresses(string address)
	{
		Assert.True(SsrfProtection.IsPrivateOrLocalAddress(IPAddress.Parse(address)));
	}

	[Theory]
	[InlineData("93.184.216.34")]
	[InlineData("8.8.8.8")]
	[InlineData("172.15.0.1")]
	[InlineData("172.32.0.1")]
	[InlineData("192.167.1.1")]
	[InlineData("100.63.255.255")]
	[InlineData("100.128.0.1")]
	[InlineData("198.17.255.255")]
	[InlineData("198.20.0.1")]
	[InlineData("223.255.255.254")]
	[InlineData("2606:2800:220:1:248:1893:25c8:1946")]
	[InlineData("::ffff:8.8.8.8")]
	public void IsPrivateOrLocalAddress_ReturnsFalse_ForPublicAddresses(string address)
	{
		Assert.False(SsrfProtection.IsPrivateOrLocalAddress(IPAddress.Parse(address)));
	}

	[Fact]
	public void CreatePageFetchHandler_ConfiguresConnectValidationAndRedirectCap()
	{
		using var handler = SsrfProtection.CreatePageFetchHandler();

		Assert.NotNull(handler.ConnectCallback);
		Assert.Equal(5, handler.MaxAutomaticRedirections);
	}

	[Theory]
	[InlineData("http://127.0.0.1:1/recipe")]
	[InlineData("http://169.254.169.254/latest/meta-data")]
	[InlineData("http://[::1]:1/recipe")]
	public async Task PageFetchHandler_BlocksPrivateAddresses_AtConnectTime(string url)
	{
		// The ConnectCallback throws before opening a socket, so no network I/O occurs.
		using var client = new HttpClient(SsrfProtection.CreatePageFetchHandler());

		var exception = await Assert.ThrowsAsync<HttpRequestException>(
			async () => await client.GetAsync(url, TestContext.Current.CancellationToken));

		var messages = $"{exception.Message} {exception.InnerException?.Message}";
		Assert.Contains("blocked", messages, StringComparison.OrdinalIgnoreCase);
	}
}
