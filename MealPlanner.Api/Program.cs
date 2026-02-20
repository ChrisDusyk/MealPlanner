using MealPlanner.Api.Features.Recipes;
using MealPlanner.Api.Features.Users;
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
	.AddJwtBearer(options =>
	{
		options.Authority = builder.Configuration["Authentication:Authority"];
		options.Audience = builder.Configuration["Authentication:Audience"];
		options.TokenValidationParameters.ValidateAudience = true;

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
app.MapUserEndpoints();

app.Run();
