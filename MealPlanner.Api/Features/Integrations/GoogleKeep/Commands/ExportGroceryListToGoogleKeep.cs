using System.Security.Cryptography;
using System.Text;
using MealPlanner.Api.Data;
using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Models;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Services;
using MealPlanner.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Commands;

public record ExportGroceryListToGoogleKeepCommand(string UserId, DateOnly WeekStart, bool ForceNewNote)
	: ICommand<GoogleKeepExportResult>;

public record GoogleKeepExportResult(
	IntegrationProvider Provider,
	string ExternalItemId,
	string WeekStart,
	DateTime ExportedAtUtc,
	bool CreatedNewItem);

public class ExportGroceryListToGoogleKeepCommandHandler(
	MealPlannerDbContext db,
	IGoogleKeepClient googleKeepClient,
	IGoogleOAuthService googleOAuthService,
	IIntegrationTokenProtector tokenProtector)
	: ICommandHandler<ExportGroceryListToGoogleKeepCommand, GoogleKeepExportResult>
{
	public async Task<Result<GoogleKeepExportResult>> HandleAsync(
		ExportGroceryListToGoogleKeepCommand command,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(command.UserId))
			return Result<GoogleKeepExportResult>.Failure(new Error(ErrorCodes.ValidationFailed, "UserId is required."));

		try
		{
			var normalizedWeek = NormalizeToMonday(command.WeekStart);
			var weekStartStr = normalizedWeek.ToString("yyyy-MM-dd");
			var listEntity = await db.GroceryLists
				.FirstOrDefaultAsync(g => g.UserId == command.UserId && g.WeekStart == weekStartStr, cancellationToken);
			if (listEntity is null)
				return Result<GoogleKeepExportResult>.Failure(new Error(ErrorCodes.NotFound, "Grocery list not found for the requested week."));

			var connection = await db.GoogleIntegrationConnections
				.FirstOrDefaultAsync(c => c.UserId == command.UserId
					&& c.Provider == IntegrationProvider.GoogleKeep.ToString()
					&& c.DisconnectedAtUtc == null, cancellationToken);
			if (connection is null)
				return Result<GoogleKeepExportResult>.Failure(new Error(ErrorCodes.NotFound, "Google Keep connection was not found."));

			var accessTokenResult = tokenProtector.Unprotect(connection.EncryptedAccessToken);
			if (!accessTokenResult.IsSuccess)
				return Result<GoogleKeepExportResult>.Failure(accessTokenResult.Error!);
			var accessToken = accessTokenResult.Value!;

			if (connection.AccessTokenExpiresAtUtc.HasValue
			    && connection.AccessTokenExpiresAtUtc.Value <= DateTime.UtcNow.AddMinutes(1)
			    && !string.IsNullOrWhiteSpace(connection.EncryptedRefreshToken))
			{
				var refreshTokenResult = tokenProtector.Unprotect(connection.EncryptedRefreshToken!);
				if (refreshTokenResult.IsSuccess)
				{
					var refreshed = await googleOAuthService.RefreshAsync(refreshTokenResult.Value!, cancellationToken);
					if (refreshed.IsSuccess)
					{
						accessToken = refreshed.Value!.AccessToken;
						var refreshToken = refreshed.Value.RefreshToken.GetValueOrDefault(refreshTokenResult.Value!);
						connection.EncryptedAccessToken = tokenProtector.Protect(accessToken);
						connection.EncryptedRefreshToken = tokenProtector.Protect(refreshToken);
						connection.AccessTokenExpiresAtUtc = refreshed.Value.ExpiresAtUtc.GetValueOrNull();
						connection.UpdatedAt = DateTime.UtcNow;
						await db.SaveChangesAsync(cancellationToken);
					}
				}
			}

			var capability = await googleKeepClient.GetCapabilityAsync(accessToken, cancellationToken);
			if (!capability.IsSuccess)
				return Result<GoogleKeepExportResult>.Failure(capability.Error!);
			if (capability.Value != IntegrationCapability.Available)
				return Result<GoogleKeepExportResult>.Failure(new Error(
					ErrorCodes.ExternalServiceError,
					$"Google Keep is not available for this account ({capability.Value})."));

			var existingLink = await db.GroceryListExportLinks.FirstOrDefaultAsync(x =>
				x.UserId == command.UserId
				&& x.WeekStart == weekStartStr
				&& x.Provider == IntegrationProvider.GoogleKeep.ToString(), cancellationToken);

			var list = MapToDomain(listEntity);
			var existingExternalId = command.ForceNewNote || existingLink is null
				? Option<string>.None()
				: Option<string>.Some(existingLink.ExternalItemId);

			var upsertResult = await googleKeepClient.UpsertGroceryListAsync(
				accessToken,
				list,
				existingExternalId,
				cancellationToken);
			if (!upsertResult.IsSuccess)
				return Result<GoogleKeepExportResult>.Failure(upsertResult.Error!);

			var hash = ComputeHash(list);
			var now = DateTime.UtcNow;

			if (existingLink is null)
			{
				existingLink = new GroceryListExportLinkEntity
				{
					Id = Guid.NewGuid(),
					UserId = command.UserId,
					GroceryListId = list.Id,
					WeekStart = weekStartStr,
					Provider = IntegrationProvider.GoogleKeep.ToString(),
					ExternalItemId = upsertResult.Value!.ExternalItemId,
					LastSyncHash = hash,
					LastExportedAtUtc = now
				};
				db.GroceryListExportLinks.Add(existingLink);
			}
			else
			{
				existingLink.GroceryListId = list.Id;
				existingLink.ExternalItemId = upsertResult.Value!.ExternalItemId;
				existingLink.LastSyncHash = hash;
				existingLink.LastExportedAtUtc = now;
			}

			await db.SaveChangesAsync(cancellationToken);

			return Result<GoogleKeepExportResult>.Success(new GoogleKeepExportResult(
				Provider: IntegrationProvider.GoogleKeep,
				ExternalItemId: upsertResult.Value!.ExternalItemId,
				WeekStart: weekStartStr,
				ExportedAtUtc: now,
				CreatedNewItem: upsertResult.Value.CreatedNewItem));
		}
		catch (Exception ex)
		{
			return Result<GoogleKeepExportResult>.Failure(new Error(
				ErrorCodes.DatabaseError,
				"Failed to export grocery list to Google Keep.",
				ex));
		}
	}

	internal static DateOnly NormalizeToMonday(DateOnly date)
	{
		var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
		return date.AddDays(-diff);
	}

	internal static GroceryList MapToDomain(GroceryListEntity doc) =>
		new(
			doc.Id.ToString(),
			doc.UserId,
			DateOnly.ParseExact(doc.WeekStart, "yyyy-MM-dd"),
			doc.Items.Select(i => new GroceryListItem(i.Name, i.Quantity, i.Unit, i.IsChecked, i.SourceRecipeNames)).ToList(),
			doc.PantryStapleItems.Select(i => new GroceryListItem(i.Name, i.Quantity, i.Unit, i.IsChecked, i.SourceRecipeNames)).ToList(),
			doc.CreatedAt,
			doc.UpdatedAt);

	internal static string ComputeHash(GroceryList list)
	{
		var raw = string.Join('|', list.Items.Select(i => $"{i.Name}:{i.Quantity}:{i.Unit}:{i.IsChecked}"))
		          + "||"
		          + string.Join('|', list.PantryStapleItems.Select(i => $"{i.Name}:{i.Quantity}:{i.Unit}:{i.IsChecked}"));
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
		return Convert.ToHexString(bytes);
	}
}
