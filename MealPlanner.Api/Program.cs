using MealPlanner.Api.Features.GroceryLists;
using MealPlanner.Api.Features.GroceryLists.Realtime;
using MealPlanner.Api.Features.MealPlans;
using MealPlanner.Api.Features.MealPlans.Realtime;
using MealPlanner.Api.Features.Recipes.Import;
using MealPlanner.Api.Features.Recipes;
using MealPlanner.Api.Features.Users;
using MealPlanner.Api.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddMongoDBClient("mealplannerDb");

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCqrsHandlers(typeof(Program).Assembly);
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, GroceryListUserIdProvider>();
builder.Services.AddScoped<IGroceryListRealtimeNotifier, GroceryListRealtimeNotifier>();
builder.Services.AddScoped<IMealPlanRealtimeNotifier, MealPlanRealtimeNotifier>();
builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection(AnthropicOptions.SectionName));
var anthropicOptionsSection = builder.Configuration.GetSection(AnthropicOptions.SectionName);
var anthropicTimeoutSeconds = anthropicOptionsSection.GetValue<int?>(nameof(AnthropicOptions.HttpTimeoutSeconds))
                              ?? 45;
var pageFetchTimeoutSeconds = anthropicOptionsSection.GetValue<int?>(nameof(AnthropicOptions.FetchTimeoutSeconds))
                              ?? 20;

builder.Services.AddHttpClient(AnthropicOptions.HttpClientName, client =>
{
	client.BaseAddress = new Uri("https://api.anthropic.com/");
	client.Timeout = TimeSpan.FromSeconds(Math.Max(5, anthropicTimeoutSeconds));
});

builder.Services.AddHttpClient(AnthropicOptions.PageFetchHttpClientName, client =>
{
	client.Timeout = TimeSpan.FromSeconds(Math.Max(5, pageFetchTimeoutSeconds));
	client.DefaultRequestHeaders.UserAgent.ParseAdd("MealPlanner/1.0 (recipe-import)");
});

builder.Services.AddScoped<IRecipePageTextExtractor, RecipePageTextExtractor>();
builder.Services.AddScoped<IClaudeIngredientExtractorClient, ClaudeIngredientExtractorClient>();
builder.Services.AddScoped<IRecipeIngredientImportService, RecipeIngredientImportService>();

// Authentication & Authorization
builder.Services.AddAuthentication()
	.AddJwtBearer(options =>
	{
		options.Authority = builder.Configuration["Authentication:Authority"];
		options.Audience = builder.Configuration["Authentication:Audience"];
		options.TokenValidationParameters.ValidateAudience = true;
		options.Events = new JwtBearerEvents
		{
			OnMessageReceived = context =>
			{
				var accessToken = context.Request.Query["access_token"];
				var path = context.HttpContext.Request.Path;

				if (!string.IsNullOrWhiteSpace(accessToken)
				    && (path.StartsWithSegments(GroceryListHub.HubRoute)
				        || path.StartsWithSegments(MealPlanHub.HubRoute)))
				{
					context.Token = accessToken;
				}

				return Task.CompletedTask;
			}
		};

		if (builder.Environment.IsDevelopment())
		{
			options.RequireHttpsMetadata = false;
		}
	});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	options.AddPolicy(RecipeEndpoints.ImportIngredientsRateLimitPolicy, httpContext =>
	{
		var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
		             ?? httpContext.User.FindFirst("sub")?.Value
		             ?? "anonymous";

		return RateLimitPartition.GetFixedWindowLimiter(
			partitionKey: userId,
			factory: _ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = 10,
				Window = TimeSpan.FromMinutes(1),
				QueueLimit = 0,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				AutoReplenishment = true
			});
	});
});

var app = builder.Build();

app.MapDefaultEndpoints();

await app.Services.EnsureUserIndexesAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
	app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapRecipeEndpoints();
app.MapMealPlanEndpoints();
app.MapGroceryListEndpoints();
app.MapUserEndpoints();
app.MapHub<GroceryListHub>(GroceryListHub.HubRoute)
	.RequireAuthorization();
app.MapHub<MealPlanHub>(MealPlanHub.HubRoute)
	.RequireAuthorization();

app.Run();
