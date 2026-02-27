using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using MealPlanner.Api.Shared;
using Microsoft.Extensions.Options;

namespace MealPlanner.Api.Features.Recipes.Import;

public sealed class RecipePageTextExtractor(
	IHttpClientFactory httpClientFactory,
	IOptions<AnthropicOptions> anthropicOptions,
	ILogger<RecipePageTextExtractor> logger)
	: IRecipePageTextExtractor
{
	private static readonly Regex RemoveNonContentElementsRegex = new(
		"<(script|style|noscript|svg|footer|header|nav|aside)[^>]*>[\\s\\S]*?</\\1>",
		RegexOptions.IgnoreCase | RegexOptions.Compiled,
		TimeSpan.FromSeconds(1));

	private static readonly Regex StripTagsRegex = new(
		"<[^>]+>",
		RegexOptions.Compiled,
		TimeSpan.FromSeconds(1));

	private static readonly Regex CollapseWhitespaceRegex = new(
		"\\s+",
		RegexOptions.Compiled,
		TimeSpan.FromSeconds(1));

	private static readonly Regex IngredientHintRegex = new(
		"\\b(ingredient|ingredients|tsp|tbsp|teaspoon|tablespoon|cup|oz|ounce|lb|g|kg|ml|l|pinch|dash|clove|slice|can|package)\\b",
		RegexOptions.IgnoreCase | RegexOptions.Compiled,
		TimeSpan.FromSeconds(1));

	private static readonly Regex QuantityHintRegex = new(
		"\\b\\d+(?:[.,]\\d+)?\\b",
		RegexOptions.Compiled,
		TimeSpan.FromSeconds(1));

	public async Task<Result<string>> ExtractAsync(string sourceUrl, CancellationToken cancellationToken = default)
	{
		if (!TryValidateUrl(sourceUrl, out var uri, out var validationError))
		{
			return Result<string>.Failure(new Error(ErrorCodes.ValidationFailed, validationError));
		}

		var addressCheck = await ValidateAddressSafetyAsync(uri, cancellationToken);
		if (!addressCheck.IsSuccess)
			return Result<string>.Failure(addressCheck.Error!);

		var options = anthropicOptions.Value;

		try
		{
			var httpClient = httpClientFactory.CreateClient(AnthropicOptions.PageFetchHttpClientName);
			using var request = new HttpRequestMessage(HttpMethod.Get, uri);
			request.Headers.Accept.ParseAdd(
				"text/html,application/xhtml+xml,application/xml;q=0.9,text/plain;q=0.8,*/*;q=0.5");

			using var response = await httpClient.SendAsync(
				request,
				HttpCompletionOption.ResponseHeadersRead,
				cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				return Result<string>.Failure(new Error(
					ErrorCodes.ValidationFailed,
					$"Unable to fetch recipe page (HTTP {(int)response.StatusCode})."));
			}

			var contentLength = response.Content.Headers.ContentLength;
			if (contentLength.HasValue && contentLength.Value > options.MaxPageBytes)
			{
				return Result<string>.Failure(new Error(
					ErrorCodes.ValidationFailed,
					$"The recipe page is too large to import (>{options.MaxPageBytes} bytes)."));
			}

			var contentResult = await ReadBodyWithSizeLimitAsync(
				response.Content,
				options.MaxPageBytes,
				cancellationToken);

			if (!contentResult.IsSuccess)
				return Result<string>.Failure(contentResult.Error!);

			var normalized = NormalizeHtmlToText(contentResult.Value ?? string.Empty);
			if (string.IsNullOrWhiteSpace(normalized))
			{
				return Result<string>.Failure(new Error(
					ErrorCodes.ValidationFailed,
					"No readable recipe content was found on the provided page."));
			}

			var maxCharacters = Math.Max(2_000, options.MaxInputCharacters);
			if (normalized.Length > maxCharacters)
				normalized = normalized[..maxCharacters];

			return Result<string>.Success(normalized);
		}
		catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
		{
			logger.LogWarning(ex, "Timed out while fetching recipe source URL {SourceUrl}", sourceUrl);
			return Result<string>.Failure(new Error(
				ErrorCodes.ExternalServiceError,
				"Timed out while fetching the recipe page."));
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to fetch or parse recipe source URL {SourceUrl}", sourceUrl);
			return Result<string>.Failure(new Error(
				ErrorCodes.ExternalServiceError,
				"Failed to fetch or parse the recipe page.",
				ex));
		}
	}

	private static bool TryValidateUrl(string sourceUrl, out Uri uri, out string errorMessage)
	{
		uri = null!;
		errorMessage = string.Empty;

		if (string.IsNullOrWhiteSpace(sourceUrl))
		{
			errorMessage = "Source URL is required.";
			return false;
		}

		if (!Uri.TryCreate(sourceUrl.Trim(), UriKind.Absolute, out var parsedUri)
		    || (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
		{
			errorMessage = "Source URL must be a valid http:// or https:// URL.";
			return false;
		}

		uri = parsedUri;

		return true;
	}

	private static async Task<Result<string>> ValidateAddressSafetyAsync(Uri uri, CancellationToken cancellationToken)
	{
		if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
		{
			return Result<string>.Failure(new Error(
				ErrorCodes.ValidationFailed,
				"Loopback or localhost URLs are not allowed."));
		}

		if (IPAddress.TryParse(uri.Host, out var literalIp) && IsPrivateOrLocalAddress(literalIp))
		{
			return Result<string>.Failure(new Error(
				ErrorCodes.ValidationFailed,
				"Private or local network URLs are not allowed."));
		}

		IPAddress[] addresses;
		try
		{
#if NET8_0_OR_GREATER
			addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
#else
			addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost);
#endif
		}
		catch
		{
			return Result<string>.Failure(new Error(
				ErrorCodes.ValidationFailed,
				"Unable to resolve recipe host address."));
		}

		if (addresses.Length == 0)
		{
			return Result<string>.Failure(new Error(
				ErrorCodes.ValidationFailed,
				"Recipe host did not resolve to a valid address."));
		}

		if (addresses.Any(IsPrivateOrLocalAddress))
		{
			return Result<string>.Failure(new Error(
				ErrorCodes.ValidationFailed,
				"Private or local network URLs are not allowed."));
		}

		return Result<string>.Success(string.Empty);
	}

	private static bool IsPrivateOrLocalAddress(IPAddress address)
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
			return address.IsIPv6LinkLocal
			       || address.IsIPv6Multicast
			       || address.IsIPv6SiteLocal
			       || address.Equals(IPAddress.IPv6Loopback);
		}

		return false;
	}

	private static async Task<Result<string>> ReadBodyWithSizeLimitAsync(
		HttpContent content,
		int maxBytes,
		CancellationToken cancellationToken)
	{
		await using var stream = await content.ReadAsStreamAsync(cancellationToken);
		using var memoryStream = new MemoryStream();
		var buffer = new byte[8192];
		long totalRead = 0;

		while (true)
		{
			var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
			if (read == 0)
				break;

			totalRead += read;
			if (totalRead > maxBytes)
			{
				return Result<string>.Failure(new Error(
					ErrorCodes.ValidationFailed,
					$"The recipe page is too large to import (>{maxBytes} bytes)."));
			}

			memoryStream.Write(buffer, 0, read);
		}

		return Result<string>.Success(Encoding.UTF8.GetString(memoryStream.ToArray()));
	}

	internal static string NormalizeHtmlToText(string html)
	{
		if (string.IsNullOrWhiteSpace(html))
			return string.Empty;

		var noHeavyTags = RemoveNonContentElementsRegex.Replace(html, " ");
		var withLineBreaks = StripTagsRegex.Replace(noHeavyTags, "\n");
		var decoded = WebUtility.HtmlDecode(withLineBreaks);

		var lines = decoded
			.Split('\n')
			.Select(line => CollapseWhitespaceRegex.Replace(line, " ").Trim())
			.Where(line => line.Length >= 2)
			.ToList();

		if (lines.Count == 0)
			return string.Empty;

		var prioritizedLines = lines
			.Where(line => IngredientHintRegex.IsMatch(line) || QuantityHintRegex.IsMatch(line))
			.Take(700)
			.ToList();

		if (prioritizedLines.Count < 40)
			prioritizedLines = lines.Take(700).ToList();

		return string.Join('\n', prioritizedLines);
	}
}
