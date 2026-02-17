var builder = DistributedApplication.CreateBuilder(args);

var mongoDb = builder.AddMongoDB("mongodb")
	.WithLifetime(ContainerLifetime.Persistent);
var mealPlannerDb = mongoDb.AddDatabase("mealplannerDb");

var keycloak = builder.AddKeycloak("keycloak", 8080)
	.WithRealmImport("./Realms")
	.WithLifetime(ContainerLifetime.Persistent);

var api = builder.AddProject<Projects.MealPlanner_Api>("api")
	.WithReference(mealPlannerDb).WaitFor(mealPlannerDb)
	.WithReference(keycloak).WaitFor(keycloak);

builder.AddViteApp("frontend", "..\\frontend")
	.WithReference(api).WaitFor(api)
	.WithReference(keycloak).WaitFor(keycloak)
	.WithPnpm()
	.WithEndpoint("http", cfg =>
	{
		cfg.Port = 3000;
		cfg.IsProxied = false;
		cfg.IsExternal = true;
	});

builder.Build().Run();
