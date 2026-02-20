var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("compose");

var mongoDb = builder.AddMongoDB("mongodb")
	.WithLifetime(ContainerLifetime.Persistent)
	.PublishAsDockerComposeService((_, service) => { service.Name = "mealplannerMongoDb"; });
var mealPlannerDb = mongoDb.AddDatabase("mealplannerDb");

var api = builder.AddProject<Projects.MealPlanner_Api>("api")
	.WithReference(mealPlannerDb).WaitFor(mealPlannerDb)
	.PublishAsDockerComposeService((_, service) => { service.Name = "mealplannerApi"; });

builder.AddViteApp("frontend", "..\\frontend")
	.WithReference(api).WaitFor(api)
	.WithEnvironment("AUTH_AUTH0_ID", builder.Configuration["AUTH_AUTH0_ID"])
	.WithEnvironment("AUTH_AUTH0_SECRET", builder.Configuration["AUTH_AUTH0_SECRET"])
	.WithEnvironment("AUTH_AUTH0_ISSUER", builder.Configuration["AUTH_AUTH0_ISSUER"])
	.WithEnvironment("AUTH_API_AUDIENCE", builder.Configuration["AUTH_API_AUDIENCE"])
	.WithEnvironment("AUTH_SECRET", builder.Configuration["AUTH_SECRET"])
	.WithPnpm()
	.WithEndpoint("http", cfg =>
	{
		cfg.Port = 3000;
		cfg.IsProxied = false;
		cfg.IsExternal = true;
	})
	.PublishAsDockerFile()
	.PublishAsDockerComposeService((_, service) => { service.Name = "mealplannerFrontend"; });

builder.Build().Run();
