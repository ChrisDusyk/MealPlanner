using MealPlanner.Api.Features.GroceryLists;
using MealPlanner.Api.Features.GroceryLists.Models;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Mappers;

public class GroceryListHelpersTests
{
	[Fact]
	public void MapToDomain_MapsAllFields()
	{
		var doc = new GroceryListDocument
		{
			Id = "g1",
			UserId = "u1",
			WeekStart = "2026-02-23",
			Items = [new GroceryListItemDocument { Name = "Rice", Quantity = 1.5m, Unit = "kg", IsChecked = true, SourceRecipeNames = ["Pilaf"] }],
			CreatedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc)
		};

		var domain = GroceryListHelpers.MapToDomain(doc);

		Assert.Equal("g1", domain.Id);
		Assert.Equal("u1", domain.UserId);
		Assert.Equal(new DateOnly(2026, 2, 23), domain.WeekStart);
		Assert.Single(domain.Items);
		Assert.True(domain.Items[0].IsChecked);
	}

	[Theory]
	[InlineData(2026, 2, 23, 2026, 2, 23)]
	[InlineData(2026, 2, 24, 2026, 2, 23)]
	[InlineData(2026, 3, 1, 2026, 2, 23)]
	public void NormalizeToMonday_NormalizesCorrectly(int y, int m, int d, int ey, int em, int ed)
	{
		var normalized = GroceryListHelpers.NormalizeToMonday(new DateOnly(y, m, d));
		Assert.Equal(new DateOnly(ey, em, ed), normalized);
	}
}
