using MealPlanner.Api.Features.GroceryLists;
using MealPlanner.Api.Features.GroceryLists.Realtime;
using MealPlanner.Api.Features.MealPlans;
using MealPlanner.Api.Features.MealPlans.Realtime;
using MealPlanner.Api.Features.Recipes;
using MealPlanner.Api.Features.Users;
using MealPlanner.Api.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;

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

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
	app.UseHttpsRedirection();
}

app.UseAuthentication();
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
