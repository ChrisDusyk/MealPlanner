using MealPlanner.Api.Data;
using MealPlanner.MigrationService;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// Railway provides DATABASE_URL in URI format; convert it to the ADO.NET key-value
// format that Npgsql expects and inject it as the named connection string.
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
	var uri = new Uri(databaseUrl);
	var userInfo = uri.UserInfo.Split(':');
	var connectionString =
		$"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]}";
	builder.Configuration["ConnectionStrings:mealplannerDb"] = connectionString;
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
