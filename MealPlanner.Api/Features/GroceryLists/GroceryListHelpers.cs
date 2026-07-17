using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Models;

namespace MealPlanner.Api.Features.GroceryLists;

/// <summary>
/// Shared helpers for the GroceryLists feature: domain mapping and date normalization.
/// </summary>
internal static class GroceryListHelpers
{
	internal static GroceryList MapToDomain(GroceryListEntity entity) =>
		new(
			Id: entity.Id.ToString(),
			FamilyGroupId: entity.FamilyGroupId.ToString(),
			WeekStart: DateOnly.ParseExact(entity.WeekStart, "yyyy-MM-dd"),
			Items: entity.Items.Select(i => new GroceryListItem(
				Name: i.Name,
				Quantity: i.Quantity,
				Unit: i.Unit,
				IsChecked: i.IsChecked,
				SourceRecipeNames: i.SourceRecipeNames
			)).ToList(),
			PantryStapleItems: entity.PantryStapleItems.Select(i => new GroceryListItem(
				Name: i.Name,
				Quantity: i.Quantity,
				Unit: i.Unit,
				IsChecked: i.IsChecked,
				SourceRecipeNames: i.SourceRecipeNames
			)).ToList(),
			CreatedAt: entity.CreatedAt,
			UpdatedAt: entity.UpdatedAt
		);

	/// <summary>
	/// Returns the Monday of the week containing <paramref name="date"/>.
	/// Consistent with how meal plans are keyed.
	/// </summary>
	internal static DateOnly NormalizeToMonday(DateOnly date)
	{
		var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
		return date.AddDays(-diff);
	}
}
