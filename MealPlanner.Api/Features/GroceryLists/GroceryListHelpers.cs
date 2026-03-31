using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Models;
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
			PantryStapleItems: doc.PantryStapleItems.Select(i => new GroceryListItem(
				Name: i.Name,
				Quantity: i.Quantity,
				Unit: i.Unit,
				IsChecked: i.IsChecked,
				SourceRecipeNames: i.SourceRecipeNames
			)).ToList(),
			CreatedAt: doc.CreatedAt,
			UpdatedAt: doc.UpdatedAt
		);

	internal static GroceryList MapToDomain(GroceryListEntity entity) =>
		new(
			Id: entity.Id.ToString(),
			UserId: entity.UserId,
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

	internal static GroceryListShare MapShareToDomain(GroceryListShareEntity entity)
	{
		if (!Enum.TryParse<SharePermission>(entity.Permission, ignoreCase: true, out var permission))
			permission = SharePermission.ReadOnly;

		return new GroceryListShare(
			Id: entity.Id.ToString(),
			OwnerUserId: entity.OwnerUserId,
			SharedWithUserId: entity.SharedWithUserId,
			WeekStart: entity.WeekStart,
			Permission: permission,
			SharedAt: entity.SharedAt,
			DismissedByRecipient: entity.DismissedByRecipient
		);
	}

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
