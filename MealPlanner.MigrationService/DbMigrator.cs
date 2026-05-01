using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.MigrationService;

internal sealed class DbMigrator(
	IServiceProvider services,
	IHostApplicationLifetime lifetime,
	ILogger<DbMigrator> logger) : BackgroundService
{
	private static readonly (string Slug, string Name)[] DefaultCatalogueTags =
	[
		("breakfast", "Breakfast"),
		("lunch", "Lunch"),
		("supper", "Supper"),
		("instant-pot", "Instant Pot"),
		("slow-cooker", "Slow Cooker"),
		("high-protein", "High Protein"),
		("gluten-free", "Gluten Free"),
		("dairy-free", "Dairy Free"),
	];

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			await using var scope = services.CreateAsyncScope();
			var db = scope.ServiceProvider.GetRequiredService<MealPlannerDbContext>();
			await db.Database.MigrateAsync(stoppingToken);
			await SeedCatalogueTagsAsync(db, logger, stoppingToken);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "An error occurred while applying database migrations.");
			Environment.Exit(1);
			return;
		}

		lifetime.StopApplication();
	}

	private static async Task SeedCatalogueTagsAsync(
		MealPlannerDbContext db,
		ILogger logger,
		CancellationToken cancellationToken)
	{
		if (await db.CatalogueTags.AnyAsync(cancellationToken))
		{
			logger.LogInformation("Catalogue tags already present; skipping seed.");
			return;
		}

		var now = DateTime.UtcNow;
		var entities = DefaultCatalogueTags
			.Select(t => new CatalogueTagEntity
			{
				Id = Guid.NewGuid(),
				Slug = t.Slug,
				Name = t.Name,
				CreatedAt = now,
			})
			.ToArray();

		db.CatalogueTags.AddRange(entities);

		try
		{
			await db.SaveChangesAsync(cancellationToken);
			logger.LogInformation("Seeded {Count} default catalogue tags.", entities.Length);
		}
		catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
		{
			logger.LogInformation(
				"Catalogue tags were seeded by another concurrent migrator instance; skipping seed.");
		}
	}
}
