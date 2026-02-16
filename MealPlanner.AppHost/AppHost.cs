var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.MealPlanner_Api>("api");

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
