var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
	.WithDataVolume()
	.WithLifetime(ContainerLifetime.Persistent)
	.WithPgWeb();
var mealPlannerDb = postgres.AddDatabase("mealplannerDb");

var migrationService = builder.AddProject<Projects.MealPlanner_MigrationService>("migration-service")
	.WithReference(mealPlannerDb)
	.WaitFor(mealPlannerDb);

var api = builder.AddProject<Projects.MealPlanner_Api>("api")
	.WithReference(mealPlannerDb)
	.WaitForCompletion(migrationService)
	.WithEnvironment("Anthropic__ApiKey",
		builder.Configuration["Anthropic__ApiKey"] ?? builder.Configuration["Anthropic:ApiKey"]);

builder.AddViteApp("frontend", "..\\frontend")
	.WithReference(api).WaitFor(api)
	.WithReference(mealPlannerDb).WaitFor(mealPlannerDb)
	.WithEnvironment("BETTER_AUTH_SECRET", builder.Configuration["BETTER_AUTH_SECRET"])
	.WithEnvironment("BETTER_AUTH_URL", builder.Configuration["BETTER_AUTH_URL"] ?? "http://localhost:3000")
	.WithEnvironment("BETTER_AUTH_JWT_AUDIENCE", builder.Configuration["BETTER_AUTH_JWT_AUDIENCE"] ?? "mealplanner-api")
	.WithEnvironment("GOOGLE_CLIENT_ID", builder.Configuration["GOOGLE_CLIENT_ID"])
	.WithEnvironment("GOOGLE_CLIENT_SECRET", builder.Configuration["GOOGLE_CLIENT_SECRET"])
	.WithPnpm()
	.PublishAsDockerFile()
	.WithEndpoint("http", cfg =>
	{
		cfg.Port = 3000;
		cfg.IsProxied = false;
		cfg.IsExternal = true;
	});

builder.Build().Run();
