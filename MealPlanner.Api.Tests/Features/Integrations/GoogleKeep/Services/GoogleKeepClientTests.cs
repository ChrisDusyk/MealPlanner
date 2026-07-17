using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Services;

namespace MealPlanner.Api.Tests.Features.Integrations.GoogleKeep.Services;

public class GoogleKeepClientTests
{
	[Fact]
	public void BuildListItemText_TruncatesToKeepLimit()
	{
		var longName = new string('a', 1200);
		var item = new GroceryListItem(longName, 1, "unit", false, []);

		var text = GoogleKeepClient.BuildListItemText(item);

		Assert.Equal(999, text.Length);
	}

	[Fact]
	public void BuildNotePayload_UsesBodyListShape()
	{
		var list = new GroceryList(
			Id: "g1",
			FamilyGroupId: TestIds.Family("u1").ToString(),
			WeekStart: new DateOnly(2026, 3, 23),
			Items: [new GroceryListItem("Milk", 1, "L", false, [])],
			PantryStapleItems: [new GroceryListItem("Salt", 1, "tsp", false, [])],
			CreatedAt: DateTime.UtcNow,
			UpdatedAt: DateTime.UtcNow);

		var payload = GoogleKeepClient.BuildNotePayload(list);

		Assert.Equal("Grocery List - 2026-03-23", payload.Title);
		Assert.NotNull(payload.Body.List);
		Assert.Null(payload.Body.Text);
		Assert.True(payload.Body.List!.ListItems.Count >= 2);
	}
}
