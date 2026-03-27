using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Commands;

namespace MealPlanner.Api.Tests.Features.Integrations.GoogleKeep.Commands;

public class ExportGroceryListToGoogleKeepTests
{
	[Fact]
	public void NormalizeToMonday_ReturnsMondayForAnyDayInWeek()
	{
		var thursday = new DateOnly(2026, 3, 26);
		var monday = ExportGroceryListToGoogleKeepCommandHandler.NormalizeToMonday(thursday);

		Assert.Equal(new DateOnly(2026, 3, 23), monday);
	}

	[Fact]
	public void ComputeHash_ChangesWhenListContentChanges()
	{
		var listA = new GroceryList(
			"g1",
			"auth0|u1",
			new DateOnly(2026, 3, 23),
			[new GroceryListItem("Milk", 1, "L", false, [])],
			[],
			DateTime.UtcNow,
			DateTime.UtcNow);

		var listB = listA with
		{
			Items = [new GroceryListItem("Milk", 2, "L", false, [])]
		};

		var hashA = ExportGroceryListToGoogleKeepCommandHandler.ComputeHash(listA);
		var hashB = ExportGroceryListToGoogleKeepCommandHandler.ComputeHash(listB);

		Assert.NotEqual(hashA, hashB);
	}
}
