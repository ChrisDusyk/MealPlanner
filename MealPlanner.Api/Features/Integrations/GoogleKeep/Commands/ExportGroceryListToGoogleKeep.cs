using System.Security.Cryptography;
using System.Text;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Models;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Services;
using MealPlanner.Api.Shared;
using MongoDB.Driver;

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
	IMongoClient mongoClient,
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
			var db = mongoClient.GetDatabase("mealplannerDb");
			var normalizedWeek = NormalizeToMonday(command.WeekStart);
			var weekStartStr = normalizedWeek.ToString("yyyy-MM-dd");
			var groceryCollection = db.GetCollection<GroceryListDocument>("grocerylists");
			var listDoc = await groceryCollection
				.Find(g => g.UserId == command.UserId && g.WeekStart == weekStartStr)
				.FirstOrDefaultAsync(cancellationToken);
			if (listDoc is null)
				return Result<GoogleKeepExportResult>.Failure(new Error(ErrorCodes.NotFound, "Grocery list not found for the requested week."));

			var connectionCollection = db.GetCollection<GoogleIntegrationConnectionDocument>(IntegrationCollections.Connections);
			var connection = await connectionCollection
				.Find(c => c.UserId == command.UserId
				           && c.Provider == IntegrationProvider.GoogleKeep.ToString()
				           && c.DisconnectedAtUtc == null)
				.FirstOrDefaultAsync(cancellationToken);
			if (connection is null)
				return Result<GoogleKeepExportResult>.Failure(new Error(ErrorCodes.NotFound, "Google Keep connection was not found."));

			var accessTokenResult = tokenProtector.Unprotect(connection.EncryptedAccessToken);
			if (!accessTokenResult.IsSuccess)
				return Result<GoogleKeepExportResult>.Failure(accessTokenResult.Error!);
			var accessToken = accessTokenResult.Value!;

			if (connection.AccessTokenExpiresAtUtc.HasValue
			    && connection.AccessTokenExpiresAtUtc.Value <= DateTime.UtcNow.AddMinutes(-1)
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
						await connectionCollection.UpdateOneAsync(
							x => x.Id == connection.Id,
							Builders<GoogleIntegrationConnectionDocument>.Update
								.Set(x => x.EncryptedAccessToken, tokenProtector.Protect(accessToken))
								.Set(x => x.EncryptedRefreshToken, tokenProtector.Protect(refreshToken))
								.Set(x => x.AccessTokenExpiresAtUtc, refreshed.Value.ExpiresAtUtc.GetValueOrNull())
								.Set(x => x.UpdatedAt, DateTime.UtcNow),
							cancellationToken: cancellationToken);
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

			var exportLinks = db.GetCollection<GroceryListExportLinkDocument>(IntegrationCollections.ExportLinks);
			var existingLink = await exportLinks.Find(x =>
				x.UserId == command.UserId
				&& x.WeekStart == weekStartStr
				&& x.Provider == IntegrationProvider.GoogleKeep.ToString()).FirstOrDefaultAsync(cancellationToken);

			var list = MapToDomain(listDoc);
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
			await exportLinks.UpdateOneAsync(
				x => x.UserId == command.UserId
				     && x.WeekStart == weekStartStr
				     && x.Provider == IntegrationProvider.GoogleKeep.ToString(),
				Builders<GroceryListExportLinkDocument>.Update
					.Set(x => x.UserId, command.UserId)
					.Set(x => x.GroceryListId, list.Id)
					.Set(x => x.WeekStart, weekStartStr)
					.Set(x => x.Provider, IntegrationProvider.GoogleKeep.ToString())
					.Set(x => x.ExternalItemId, upsertResult.Value!.ExternalItemId)
					.Set(x => x.LastSyncHash, hash)
					.Set(x => x.LastExportedAtUtc, now),
				new UpdateOptions { IsUpsert = true },
				cancellationToken);

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

	internal static GroceryList MapToDomain(GroceryListDocument doc) =>
		new(
			doc.Id ?? string.Empty,
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
