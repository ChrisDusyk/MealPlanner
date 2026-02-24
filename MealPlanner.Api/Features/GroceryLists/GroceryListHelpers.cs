using MealPlanner.Api.Features.GroceryLists.Models;

namespace MealPlanner.Api.Features.GroceryLists;

/// <summary>
/// Shared helpers for the GroceryLists feature: domain mapping and date normalization.
/// </summary>
internal static class GroceryListHelpers
{
	/// <summary>
	/// Maps a persistence document to the domain model.
	/// </summary>
	internal static GroceryList MapToDomain(GroceryListDocument doc) =>
		new(
			Id: doc.Id!,
			UserId: doc.UserId,
			WeekStart: DateOnly.ParseExact(doc.WeekStart, "yyyy-MM-dd"),
			Items: doc.Items.Select(i => new GroceryListItem(
				Name: i.Name,
				Quantity: i.Quantity,
				Unit: i.Unit,
				IsChecked: i.IsChecked,
				SourceRecipeNames: i.SourceRecipeNames
			)).ToList(),
			CreatedAt: doc.CreatedAt,
			UpdatedAt: doc.UpdatedAt
		);

	/// <summary>
	/// Returns the Monday of the week containing <paramref name="date"/>.
	/// Consistent with how meal plan documents are keyed.
	/// </summary>
	internal static DateOnly NormalizeToMonday(DateOnly date)
	{
		var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
		return date.AddDays(-diff);
	}
}
