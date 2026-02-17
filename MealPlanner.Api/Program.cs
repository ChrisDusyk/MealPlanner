using MealPlanner.Api.Features.Recipes;
using MealPlanner.Api.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddMongoDBClient("mealplannerDb");

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCqrsHandlers(typeof(Program).Assembly);

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapRecipeEndpoints();

app.Run();
