using MealPlanner.Api.Data;
using Microsoft.EntityFrameworkCore;
using MealPlanner.Api.Features.GroceryLists;
using MealPlanner.Api.Features.GroceryLists.Realtime;
using MealPlanner.Api.Features.Admin;
using MealPlanner.Api.Features.Auth;
using MealPlanner.Api.Features.Integrations.GoogleKeep;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Options;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Services;
using MealPlanner.Api.Features.MealPlans;
using MealPlanner.Api.Features.MealPlans.Realtime;
using MealPlanner.Api.Features.Recipes.Import;
using MealPlanner.Api.Features.Recipes;
using MealPlanner.Api.Features.Users;
using MealPlanner.Api.Features.Users.Realtime;
using MealPlanner.Api.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<MealPlannerDbContext>(
	"mealplannerDb",
	configureDbContextOptions: options =>
		options.UseNpgsql(npgsql =>
			npgsql.ConfigureDataSource(dataSourceBuilder => dataSourceBuilder.EnableDynamicJson())));

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCqrsHandlers(typeof(Program).Assembly);
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, GroceryListUserIdProvider>();
builder.Services.AddScoped<IGroceryListRealtimeNotifier, GroceryListRealtimeNotifier>();
builder.Services.AddScoped<IMealPlanRealtimeNotifier, MealPlanRealtimeNotifier>();
builder.Services.AddScoped<IFriendsRealtimeNotifier, FriendsRealtimeNotifier>();
builder.Services.Configure<GoogleIntegrationsOptions>(
	builder.Configuration.GetSection(GoogleIntegrationsOptions.SectionName));
var googleIntegrationsSection = builder.Configuration.GetSection(GoogleIntegrationsOptions.SectionName);
var dataProtectionApplicationName = googleIntegrationsSection.GetValue<string>(nameof(GoogleIntegrationsOptions.DataProtectionApplicationName))
	?? "MealPlanner.Api";
var dataProtectionKeyRingPath = googleIntegrationsSection.GetValue<string>(nameof(GoogleIntegrationsOptions.DataProtectionKeyRingPath));
var dataProtectionBuilder = builder.Services
	.AddDataProtection()
	.SetApplicationName(dataProtectionApplicationName);
if (!string.IsNullOrWhiteSpace(dataProtectionKeyRingPath))
{
	dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyRingPath));
}

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

builder.Services.AddHttpClient(GoogleIntegrationsOptions.OAuthHttpClientName, client =>
{
	client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient(GoogleIntegrationsOptions.KeepHttpClientName, client =>
{
	client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
builder.Services.AddScoped<IGoogleKeepClient, GoogleKeepClient>();
builder.Services.AddSingleton<IIntegrationTokenProtector, IntegrationTokenProtector>();

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
				        || path.StartsWithSegments(MealPlanHub.HubRoute)
				        || path.StartsWithSegments(FriendsHub.HubRoute)))
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
builder.Services.AddAuthorization(options =>
{
	options.AddPolicy(RbacAuthorization.RequireUserRolePolicy, policy =>
		policy.RequireAuthenticatedUser()
			.RequireAssertion(context => RbacAuthorization.IsUserAuthorized(context.User)));

	options.AddPolicy(RbacAuthorization.RequireAdminRolePolicy, policy =>
		policy.RequireAuthenticatedUser()
			.RequireAssertion(context => RbacAuthorization.IsAdminAuthorized(context.User)));
});
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
app.MapGoogleKeepEndpoints();
app.MapAdminEndpoints();
app.MapHub<GroceryListHub>(GroceryListHub.HubRoute)
	.RequireAuthorization(RbacAuthorization.RequireUserRolePolicy);
app.MapHub<MealPlanHub>(MealPlanHub.HubRoute)
	.RequireAuthorization(RbacAuthorization.RequireUserRolePolicy);
app.MapHub<FriendsHub>(FriendsHub.HubRoute)
	.RequireAuthorization(RbacAuthorization.RequireUserRolePolicy);

app.Run();
