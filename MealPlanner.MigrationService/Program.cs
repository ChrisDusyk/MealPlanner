using MealPlanner.Api.Data;
using MealPlanner.MigrationService;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<MealPlannerDbContext>(
	"mealplannerDb",
	configureDbContextOptions: options =>
		options.UseNpgsql(npgsql =>
			npgsql.ConfigureDataSource(dataSourceBuilder => dataSourceBuilder.EnableDynamicJson())));

builder.Services.AddHostedService<DbMigrator>();

var app = builder.Build();

app.Run();
