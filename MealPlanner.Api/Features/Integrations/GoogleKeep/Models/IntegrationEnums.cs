namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Models;

public enum IntegrationProvider
{
	GoogleKeep = 0,
	GoogleTasks = 1
}

public enum IntegrationCapability
{
	Unknown = 0,
	Available = 1,
	UnsupportedForAccount = 2,
	ForbiddenByAdmin = 3
}
