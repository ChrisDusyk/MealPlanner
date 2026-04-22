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
	.WithEnvironment("AUTH_AUTH0_ID", builder.Configuration["AUTH_AUTH0_ID"])
	.WithEnvironment("AUTH_AUTH0_SECRET", builder.Configuration["AUTH_AUTH0_SECRET"])
	.WithEnvironment("AUTH_AUTH0_ISSUER", builder.Configuration["AUTH_AUTH0_ISSUER"])
	.WithEnvironment("AUTH_API_AUDIENCE", builder.Configuration["AUTH_API_AUDIENCE"])
	.WithEnvironment("AUTH_SECRET", builder.Configuration["AUTH_SECRET"])
	.WithPnpm()
	.PublishAsDockerFile()
	.WithEndpoint("http", cfg =>
	{
		cfg.Port = 3000;
		cfg.IsProxied = false;
		cfg.IsExternal = true;
	});

builder.Build().Run();
