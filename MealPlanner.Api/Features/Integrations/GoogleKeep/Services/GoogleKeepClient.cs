using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Models;
using MealPlanner.Api.Shared;

namespace MealPlanner.Api.Features.Integrations.GoogleKeep.Services;

public sealed class GoogleKeepClient(
	IHttpClientFactory httpClientFactory,
	ILogger<GoogleKeepClient> logger)
	: IGoogleKeepClient
{
	private const int MaxNoteTitleLength = 999;
	private const int MaxListItemTextLength = 999;
	private const int MaxListItems = 1000;

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<Result<IntegrationCapability>> GetCapabilityAsync(
		string accessToken,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(accessToken))
			return Result<IntegrationCapability>.Failure(new Error(ErrorCodes.ValidationFailed, "Access token is required."));

		try
		{
			var httpClient = httpClientFactory.CreateClient(Options.GoogleIntegrationsOptions.KeepHttpClientName);
			using var request = new HttpRequestMessage(HttpMethod.Get, "https://keep.googleapis.com/v1/notes?pageSize=1");
			request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
			var response = await httpClient.SendAsync(request, cancellationToken);
			if (response.IsSuccessStatusCode)
				return Result<IntegrationCapability>.Success(IntegrationCapability.Available);

			if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
				return Result<IntegrationCapability>.Success(IntegrationCapability.ForbiddenByAdmin);
			if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
				return Result<IntegrationCapability>.Success(IntegrationCapability.UnsupportedForAccount);
			if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
				return Result<IntegrationCapability>.Failure(new Error(ErrorCodes.Unauthorized, "Google access token is not valid."));

			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			logger.LogWarning("Google Keep capability probe failed with status {StatusCode}: {Body}", (int)response.StatusCode, body);
			return Result<IntegrationCapability>.Failure(new Error(
				ErrorCodes.ExternalServiceError,
				"Google Keep capability probe failed."));
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Google Keep capability probe failed.");
			return Result<IntegrationCapability>.Failure(new Error(
				ErrorCodes.ExternalServiceError,
				"Google Keep capability probe failed.",
				ex));
		}
	}

	public async Task<Result<GoogleKeepUpsertResult>> UpsertGroceryListAsync(
		string accessToken,
		GroceryList list,
		Option<string> existingExternalItemId,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(accessToken))
			return Result<GoogleKeepUpsertResult>.Failure(new Error(ErrorCodes.ValidationFailed, "Access token is required."));

		var payload = JsonSerializer.Serialize(BuildNotePayload(list), JsonOptions);

		try
		{
			var httpClient = httpClientFactory.CreateClient(Options.GoogleIntegrationsOptions.KeepHttpClientName);
			if (existingExternalItemId.HasValue)
			{
				var patchResult = await PatchNoteAsync(httpClient, accessToken, existingExternalItemId.Value, payload, cancellationToken);
				if (patchResult.IsSuccess || patchResult.Error?.Code != ErrorCodes.NotFound)
					return patchResult;
			}

			using var createRequest = new HttpRequestMessage(HttpMethod.Post, "https://keep.googleapis.com/v1/notes");
			createRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");
			createRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
			var createResponse = await httpClient.SendAsync(createRequest, cancellationToken);
			var createBody = await createResponse.Content.ReadAsStringAsync(cancellationToken);
			if (!createResponse.IsSuccessStatusCode)
			{
				logger.LogWarning("Google Keep note create failed: {StatusCode} {Body}", (int)createResponse.StatusCode, createBody);
				return Result<GoogleKeepUpsertResult>.Failure(new Error(
					ErrorCodes.ExternalServiceError,
					"Failed to create Google Keep note."));
			}

			var id = ReadNoteId(createBody);
			if (string.IsNullOrWhiteSpace(id))
			{
				return Result<GoogleKeepUpsertResult>.Failure(new Error(
					ErrorCodes.ExternalServiceError,
					"Google Keep create response did not include note id."));
			}

			return Result<GoogleKeepUpsertResult>.Success(new GoogleKeepUpsertResult(id, true));
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to upsert Google Keep note.");
			return Result<GoogleKeepUpsertResult>.Failure(new Error(
				ErrorCodes.ExternalServiceError,
				"Failed to upsert Google Keep note.",
				ex));
		}
	}

	private static async Task<Result<GoogleKeepUpsertResult>> PatchNoteAsync(
		HttpClient httpClient,
		string accessToken,
		string noteId,
		string payload,
		CancellationToken cancellationToken)
	{
		using var patchRequest = new HttpRequestMessage(
			HttpMethod.Patch,
			$"https://keep.googleapis.com/v1/{noteId}?updateMask=title,body");
		patchRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");
		patchRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
		var patchResponse = await httpClient.SendAsync(patchRequest, cancellationToken);
		if (!patchResponse.IsSuccessStatusCode)
		{
			if (patchResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
			{
				return Result<GoogleKeepUpsertResult>.Failure(new Error(
					ErrorCodes.NotFound,
					"Existing Google Keep note no longer exists."));
			}

			return Result<GoogleKeepUpsertResult>.Failure(new Error(
				ErrorCodes.ExternalServiceError,
				"Failed to update Google Keep note."));
		}

		return Result<GoogleKeepUpsertResult>.Success(new GoogleKeepUpsertResult(noteId, false));
	}

	internal static KeepNotePayload BuildNotePayload(GroceryList list)
	{
		var listItems = new List<KeepNoteListItem>();
		foreach (var item in list.Items)
		{
			if (listItems.Count >= MaxListItems)
				break;

			listItems.Add(new KeepNoteListItem(
				new KeepNoteTextContent(BuildListItemText(item)),
				item.IsChecked,
				[]));
		}

		if (list.PantryStapleItems.Count > 0)
		{
			if (listItems.Count < MaxListItems)
			{
				listItems.Add(new KeepNoteListItem(
					new KeepNoteTextContent("Pantry staples"),
					Checked: false,
					[]));
			}

			foreach (var item in list.PantryStapleItems)
			{
				if (listItems.Count >= MaxListItems)
					break;

				listItems.Add(new KeepNoteListItem(
					new KeepNoteTextContent(BuildListItemText(item)),
					item.IsChecked,
					[]));
			}
		}

		if (listItems.Count == 0)
		{
			listItems.Add(new KeepNoteListItem(new KeepNoteTextContent("No grocery items"), false, []));
		}

		var title = Truncate($"Grocery List - {list.WeekStart:yyyy-MM-dd}", MaxNoteTitleLength);
		return new KeepNotePayload(
			title,
			new KeepNoteSection(
				null,
				new KeepNoteListContent(listItems)));
	}

	internal static string BuildListItemText(GroceryListItem item)
	{
		var sb = new StringBuilder();
		sb.Append(item.Name.Trim());
		if (item.Quantity > 0)
			sb.Append(' ').Append(item.Quantity);
		if (!string.IsNullOrWhiteSpace(item.Unit))
			sb.Append(' ').Append(item.Unit.Trim());

		var text = sb.ToString().Trim();
		return Truncate(string.IsNullOrWhiteSpace(text) ? "Unnamed item" : text, MaxListItemTextLength);
	}

	private static string Truncate(string value, int maxLength)
	{
		if (value.Length <= maxLength)
			return value;

		return value[..maxLength];
	}

	private static string ReadNoteId(string responseBody)
	{
		try
		{
			using var doc = JsonDocument.Parse(responseBody);
			if (doc.RootElement.TryGetProperty("name", out var nameElement))
				return nameElement.GetString() ?? string.Empty;

			return string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}

	internal sealed record KeepNotePayload(
		[property: JsonPropertyName("title")] string Title,
		[property: JsonPropertyName("body")] KeepNoteSection Body);

	internal sealed record KeepNoteSection(
		[property: JsonPropertyName("text")] KeepNoteTextContent? Text,
		[property: JsonPropertyName("list")] KeepNoteListContent? List);

	internal sealed record KeepNoteTextContent(
		[property: JsonPropertyName("text")] string Text);

	internal sealed record KeepNoteListContent(
		[property: JsonPropertyName("listItems")] IReadOnlyList<KeepNoteListItem> ListItems);

	internal sealed record KeepNoteListItem(
		[property: JsonPropertyName("text")] KeepNoteTextContent Text,
		[property: JsonPropertyName("checked")] bool Checked,
		[property: JsonPropertyName("childListItems")] IReadOnlyList<KeepNoteListItem> ChildListItems);
}
