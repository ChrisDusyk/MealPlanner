var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("compose");

var mongoDb = builder.AddMongoDB("mongodb")
	.WithLifetime(ContainerLifetime.Persistent)
	.PublishAsDockerComposeService((resource, service) => { service.Name = "mealplannerMongoDb"; });
var mealPlannerDb = mongoDb.AddDatabase("mealplannerDb");

var keycloak = builder.AddKeycloak("keycloak", 8080)
	.WithRealmImport("./Realms")
	.WithLifetime(ContainerLifetime.Persistent)
	.PublishAsDockerComposeService((resource, service) => { service.Name = "mealplannerKeycloak"; });

var api = builder.AddProject<Projects.MealPlanner_Api>("api")
	.WithReference(mealPlannerDb).WaitFor(mealPlannerDb)
	.WithReference(keycloak).WaitFor(keycloak)
	.PublishAsDockerComposeService((resource, service) => { service.Name = "mealplannerApi"; });

builder.AddViteApp("frontend", "..\\frontend")
	.WithReference(api).WaitFor(api)
	.WithReference(keycloak).WaitFor(keycloak)
	.WithPnpm()
	.WithEndpoint("http", cfg =>
	{
		cfg.Port = 3000;
		cfg.IsProxied = false;
		cfg.IsExternal = true;
	})
	.PublishAsDockerComposeService((resource, service) => { service.Name = "mealplannerFrontend"; });

builder.Build().Run();
