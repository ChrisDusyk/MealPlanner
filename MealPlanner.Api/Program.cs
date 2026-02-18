using MealPlanner.Api.Features.Recipes;
using MealPlanner.Api.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddMongoDBClient("mealplannerDb");

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCqrsHandlers(typeof(Program).Assembly);

// Authentication & Authorization
builder.Services.AddAuthentication()
	.AddKeycloakJwtBearer(
		serviceName: "keycloak",
		realm: "mealplanner",
		options =>
		{
			options.Audience = "mealplanner-api";
			if (builder.Environment.IsDevelopment())
			{
				options.RequireHttpsMetadata = false;
				// Aspire service discovery resolves Keycloak to an internal URL,
				// but tokens are issued with the external URL (http://localhost:8080).
				// Disable strict issuer validation in dev to handle this mismatch.
				options.TokenValidationParameters.ValidateIssuer = false;
			}
		});
builder.Services.AddAuthorization();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapRecipeEndpoints();

app.Run();
