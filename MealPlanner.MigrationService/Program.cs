using MealPlanner.Api.Data;
using MealPlanner.Api.Shared;
using MealPlanner.MigrationService;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// Railway provides DATABASE_URL in URI format; convert it to the ADO.NET key-value
// format that Npgsql expects and inject it as the named connection string.
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
	builder.Configuration["ConnectionStrings:mealplannerDb"] = DatabaseUrlParser.ToConnectionString(databaseUrl);
}

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<MealPlannerDbContext>(
	"mealplannerDb",
	configureDbContextOptions: options =>
		options.UseNpgsql(npgsql =>
			npgsql.ConfigureDataSource(dataSourceBuilder => dataSourceBuilder.EnableDynamicJson())));

builder.Services.AddHostedService<DbMigrator>();

var app = builder.Build();

app.Run();
