using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Tests.TestUtilities;
using MealPlanner.Api.Features.GroceryLists;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Mappers;

public class GroceryListHelpersTests
{
	[Fact]
	public void MapToDomain_MapsAllFields()
	{
		var entity = new GroceryListEntity
		{
			Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
			FamilyGroupId = TestIds.Family("u1"),
			WeekStart = "2026-02-23",
			Items = [new GroceryListItemData { Name = "Rice", Quantity = 1.5m, Unit = "kg", IsChecked = true, SourceRecipeNames = ["Pilaf"] }],
			PantryStapleItems = [new GroceryListItemData { Name = "Salt", Quantity = 1m, Unit = "tsp", IsChecked = false, SourceRecipeNames = ["Pasta"] }],
			CreatedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
			UpdatedAt = new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc)
		};

		var domain = GroceryListHelpers.MapToDomain(entity);

		Assert.Equal("11111111-1111-1111-1111-111111111111", domain.Id);
		Assert.Equal(TestIds.Family("u1").ToString(), domain.FamilyGroupId);
		Assert.Equal(new DateOnly(2026, 2, 23), domain.WeekStart);
		Assert.Single(domain.Items);
		Assert.True(domain.Items[0].IsChecked);
		Assert.Single(domain.PantryStapleItems);
		Assert.Equal("Salt", domain.PantryStapleItems[0].Name);
		Assert.Equal(1m, domain.PantryStapleItems[0].Quantity);
		Assert.Equal("tsp", domain.PantryStapleItems[0].Unit);
		Assert.False(domain.PantryStapleItems[0].IsChecked);
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
