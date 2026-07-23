var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("mealplanner")
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

// flagd is the OpenFeature evaluation engine. It HTTP-syncs its flag
// definitions from the API's internal endpoint (so toggles are live without a
// redeploy) and serves evaluations to the API and frontend over gRPC.
var flagd = builder.AddFlagd("flagd")
	.WithReference(api)
	.WaitFor(api)
	.WithArgs(context =>
	{
		context.Args.Add("--uri");
		context.Args.Add(ReferenceExpression.Create($"{api.GetEndpoint("http")}/internal/feature-flags"));
	});

// The API resolves flags against flagd (injected as connection string "flagd").
// Deliberately no WaitFor(flagd): the OpenFeature provider connects lazily and
// retries, which avoids a start-order deadlock with flagd waiting for the API.
api.WithReference(flagd);

var frontend = builder.AddViteApp("frontend", "../frontend")
	.WithReference(api).WaitFor(api)
	.WithReference(mealPlannerDb).WaitFor(mealPlannerDb)
	.WithReference(flagd).WaitFor(flagd)
	.WithEnvironment("FLAGD_HOST",
		ReferenceExpression.Create($"{flagd.GetEndpoint("http").Property(EndpointProperty.Host)}"))
	.WithEnvironment("FLAGD_PORT",
		ReferenceExpression.Create($"{flagd.GetEndpoint("http").Property(EndpointProperty.Port)}"))
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

var frontendAuthMigrator = builder.AddExecutable(
		"frontend-auth-migrator",
		"pnpm",
		"../frontend",
		"run", "migrate:auth")
	.WithParentRelationship(frontend.Resource)
	.ExcludeFromManifest()
	.WithReference(mealPlannerDb).WaitFor(mealPlannerDb)
	.WithEnvironment("BETTER_AUTH_SECRET", builder.Configuration["BETTER_AUTH_SECRET"])
	.WithEnvironment("BETTER_AUTH_URL", builder.Configuration["BETTER_AUTH_URL"] ?? "http://localhost:3000")
	.WithEnvironment("BETTER_AUTH_JWT_AUDIENCE", builder.Configuration["BETTER_AUTH_JWT_AUDIENCE"] ?? "mealplanner-api")
	.WithEnvironment("GOOGLE_CLIENT_ID", builder.Configuration["GOOGLE_CLIENT_ID"])
	.WithEnvironment("GOOGLE_CLIENT_SECRET", builder.Configuration["GOOGLE_CLIENT_SECRET"]);

frontend.WaitForCompletion(frontendAuthMigrator);

builder.Build().Run();
