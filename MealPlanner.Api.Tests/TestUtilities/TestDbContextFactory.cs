using MealPlanner.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Tests.TestUtilities;

internal static class TestDbContextFactory
{
	internal static MealPlannerDbContext CreateContext(string? databaseName = null)
	{
		var options = new DbContextOptionsBuilder<MealPlannerDbContext>()
			.UseInMemoryDatabase(databaseName ?? $"mealplanner-tests-{Guid.NewGuid():N}")
			.Options;

		return new MealPlannerDbContext(options);
	}

	internal static MealPlannerDbContext CreateContext(Action<MealPlannerDbContext> seed, string? databaseName = null)
	{
		var context = CreateContext(databaseName);
		seed(context);
		context.SaveChanges();
		return context;
	}
}
