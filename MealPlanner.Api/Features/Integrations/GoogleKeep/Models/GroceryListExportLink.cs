using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Models;

public record GroceryListExportLink(
	string Id,
	string UserId,
	string GroceryListId,
	string WeekStart,
	IntegrationProvider Provider,
	string ExternalItemId,
	DateTime LastExportedAtUtc,
	Option<string> LastSyncHash
);
