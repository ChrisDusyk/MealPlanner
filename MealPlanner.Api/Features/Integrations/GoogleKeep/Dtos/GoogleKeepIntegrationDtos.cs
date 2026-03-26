using MealPlanner.Api.Features.Integrations.GoogleKeep.Commands;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Models;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Queries;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Dtos;

public record StartGoogleKeepConnectionRequest(string RedirectBaseUri);

public record CompleteGoogleKeepConnectionRequest(string Code, string State);

public record ExportGroceryListToGoogleKeepRequest(string WeekStart, bool ForceNewNote = false);

public record StartGoogleKeepConnectionResponse(string AuthorizationUrl, string State)
{
	public static StartGoogleKeepConnectionResponse FromDomain(StartGoogleKeepConnectionResult result) =>
		new(result.AuthorizationUrl, result.State);
}

public record GoogleKeepConnectionStatusResponse(
	bool IsConnected,
	string Provider,
	string Capability,
	string? ConnectedEmail,
	DateTime? ConnectedAtUtc)
{
	public static GoogleKeepConnectionStatusResponse FromDomain(GoogleKeepConnectionStatus status) =>
		new(
			status.IsConnected,
			status.Provider.ToString(),
			status.Capability.ToString(),
			status.ConnectedEmail.GetValueOrNull(),
			status.ConnectedAtUtc.GetValueOrNull());
}

public record GoogleKeepExportResponse(
	string Provider,
	string ExternalItemId,
	string WeekStart,
	DateTime ExportedAtUtc,
	bool CreatedNewItem)
{
	public static GoogleKeepExportResponse FromDomain(GoogleKeepExportResult result) =>
		new(
			result.Provider.ToString(),
			result.ExternalItemId,
			result.WeekStart,
			result.ExportedAtUtc,
			result.CreatedNewItem);
}
