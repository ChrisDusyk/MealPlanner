var builder = DistributedApplication.CreateBuilder(args);

var mongoDb = builder.AddMongoDB("mongodb")
	.WithLifetime(ContainerLifetime.Persistent);
var mealPlannerDb = mongoDb.AddDatabase("mealplannerDb");

var api = builder.AddProject<Projects.MealPlanner_Api>("api")
	.WithReference(mealPlannerDb).WaitFor(mealPlannerDb);

builder.AddViteApp("frontend", "..\\frontend")
	.WithReference(api).WaitFor(api)
	.WithPnpm()
	.WithEndpoint("http", cfg =>
	{
		cfg.Port = 3000;
		cfg.IsProxied = false;
		cfg.IsExternal = true;
	});

builder.Build().Run();
