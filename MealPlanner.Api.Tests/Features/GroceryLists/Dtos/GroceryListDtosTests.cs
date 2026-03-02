using MealPlanner.Api.Features.GroceryLists.Dtos;
using MealPlanner.Api.Features.GroceryLists.Models;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Dtos;

public class GroceryListDtosTests
{
	[Fact]
	public void GroceryListItemDto_FromDomain_MapsAllFields()
	{
		var item = new GroceryListItem("Rice", 1.5m, "kg", true, ["Pilaf"]);

		var dto = GroceryListItemDto.FromDomain(item);

		Assert.Equal("Rice", dto.Name);
		Assert.Equal(1.5m, dto.Quantity);
		Assert.Equal("kg", dto.Unit);
		Assert.True(dto.IsChecked);
		Assert.Single(dto.SourceRecipeNames);
	}

	[Fact]
	public void GroceryListResponse_FromDomain_MapsWeekAndItems()
	{
		var list = new GroceryList(
			"g1",
			"u1",
			new DateOnly(2026, 2, 23),
			[new GroceryListItem("Rice", 1.5m, "kg", false, [])],
			[],
			new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc));

		var response = GroceryListResponse.FromDomain(list);

		Assert.Equal("g1", response.Id);
		Assert.Equal("2026-02-23", response.WeekStart);
		Assert.Single(response.Items);
		Assert.Empty(response.PantryStapleItems);
	}

	[Fact]
	public void GroceryListResponse_FromDomain_MapsPantryStapleItems()
	{
		var list = new GroceryList(
			"g1",
			"u1",
			new DateOnly(2026, 2, 23),
			[new GroceryListItem("Rice", 1.5m, "kg", false, [])],
			[new GroceryListItem("Salt", 1m, "tsp", false, ["Pasta"])],
			new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
			new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc));

		var response = GroceryListResponse.FromDomain(list);

		Assert.Single(response.Items);
		Assert.Single(response.PantryStapleItems);
		Assert.Equal("Salt", response.PantryStapleItems[0].Name);
		Assert.Equal(1m, response.PantryStapleItems[0].Quantity);
		Assert.Equal("tsp", response.PantryStapleItems[0].Unit);
		Assert.Single(response.PantryStapleItems[0].SourceRecipeNames);
	}
}
