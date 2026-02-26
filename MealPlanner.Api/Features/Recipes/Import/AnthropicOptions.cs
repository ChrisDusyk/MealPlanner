namespace MealPlanner.Api.Features.Recipes.Import;

public sealed class AnthropicOptions
{
	public const string SectionName = "Anthropic";
	public const string HttpClientName = "Anthropic";
	public const string PageFetchHttpClientName = "RecipeImportPageFetch";

	public string ApiKey { get; set; } = string.Empty;

	public int MaxInputCharacters { get; set; } = 20000;

	public int MaxPageBytes { get; set; } = 1_500_000;

	public int MaxIngredients { get; set; } = 100;

	public int MaxOutputTokens { get; set; } = 1400;

	public int HttpTimeoutSeconds { get; set; } = 45;

	public int FetchTimeoutSeconds { get; set; } = 20;
}
