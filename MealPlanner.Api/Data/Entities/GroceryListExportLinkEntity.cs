namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for a grocery list export link.
/// </summary>
public class GroceryListExportLinkEntity
{
	public Guid Id { get; set; }

	public string UserId { get; set; } = string.Empty;

	public string GroceryListId { get; set; } = string.Empty;

	public string WeekStart { get; set; } = string.Empty;

	public string Provider { get; set; } = string.Empty;

	public string ExternalItemId { get; set; } = string.Empty;

	public DateTime LastExportedAtUtc { get; set; }

	public string? LastSyncHash { get; set; }
}
