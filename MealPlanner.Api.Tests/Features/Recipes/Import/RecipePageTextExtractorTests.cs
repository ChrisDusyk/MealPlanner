using System.Net;
using MealPlanner.Api.Features.Recipes.Import;
using MealPlanner.Api.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MealPlanner.Api.Tests.Features.Recipes.Import;

public class RecipePageTextExtractorTests
{
	private static readonly AnthropicOptions DefaultOptions = new()
	{
		MaxPageBytes = 1_500_000,
		MaxInputCharacters = 20_000
	};

	[Fact]
	public async Task ExtractAsync_ReturnsValidationFailure_WhenUrlIsInvalid()
	{
		var extractor = CreateSut((_, _) => throw new InvalidOperationException("Should not send request."));

		var result = await extractor.ExtractAsync("not-a-url", TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
	}

	[Fact]
	public async Task ExtractAsync_ReturnsValidationFailure_WhenLoopbackUrlProvided()
	{
		var extractor = CreateSut((_, _) => throw new InvalidOperationException("Should not send request."));

		var result = await extractor.ExtractAsync("http://localhost:5000/recipe", TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
	}

	[Fact]
	public async Task ExtractAsync_ReturnsExternalServiceError_WhenFetchTimesOut()
	{
		var extractor = CreateSut((_, _) => throw new TaskCanceledException("Timed out"));

		var result = await extractor.ExtractAsync("https://93.184.216.34/recipe", TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ExternalServiceError, result.Error.Code);
	}

	[Fact]
	public async Task ExtractAsync_ReturnsExternalServiceError_WhenFetchThrowsUnexpectedException()
	{
		var extractor = CreateSut((_, _) => throw new HttpRequestException("Boom"));

		var result = await extractor.ExtractAsync("https://93.184.216.34/recipe", TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ExternalServiceError, result.Error.Code);
	}

	[Fact]
	public async Task ExtractAsync_ReturnsNormalizedText_WhenFetchSucceeds()
	{
		var html =
			"""
			<html>
			  <body>
			    <script>ignore me</script>
			    <h1>Best Pancakes</h1>
			    <ul>
			      <li>2 cups flour</li>
			      <li>1 tsp salt</li>
			    </ul>
			  </body>
			</html>
			""";

		var extractor = CreateSut((_, _) =>
			Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(html)
			}));

		var result = await extractor.ExtractAsync("https://93.184.216.34/recipe", TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Contains("2 cups flour", result.Value, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("1 tsp salt", result.Value, StringComparison.OrdinalIgnoreCase);
	}

	private static RecipePageTextExtractor CreateSut(
		Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc,
		AnthropicOptions? options = null)
	{
		var handler = new StubHttpMessageHandler(handlerFunc);
		var httpClient = new HttpClient(handler);
		var factory = new Mock<IHttpClientFactory>();
		factory
			.Setup(x => x.CreateClient(AnthropicOptions.PageFetchHttpClientName))
			.Returns(httpClient);

		return new RecipePageTextExtractor(
			factory.Object,
			Options.Create(options ?? DefaultOptions),
			Mock.Of<ILogger<RecipePageTextExtractor>>());
	}

	private sealed class StubHttpMessageHandler(
		Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc)
		: HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken) => handlerFunc(request, cancellationToken);
	}
}
