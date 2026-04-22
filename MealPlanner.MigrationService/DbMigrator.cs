using MealPlanner.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.MigrationService;

internal sealed class DbMigrator(
	IServiceProvider services,
	IHostApplicationLifetime lifetime,
	ILogger<DbMigrator> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			await using var scope = services.CreateAsyncScope();
			var db = scope.ServiceProvider.GetRequiredService<MealPlannerDbContext>();
			await db.Database.MigrateAsync(stoppingToken);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "An error occurred while applying database migrations.");
			Environment.Exit(1);
			return;
		}

		lifetime.StopApplication();
	}
}
