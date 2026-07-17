using System.Security.Claims;
using MealPlanner.Api.Data;
using MealPlanner.Api.Features.GroceryLists.Dtos;
using MealPlanner.Api.Features.GroceryLists.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.GroceryLists.Realtime;

public enum GroceryListRealtimeEventType
{
	ItemToggled,
	CustomItemAdded,
	PantryStaplePromoted
}

public record GroceryListUpdatedEvent(
	string EventType,
	string FamilyGroupId,
	string WeekStart,
	GroceryListResponse GroceryList,
	string ChangedByUserId,
	DateTime OccurredAt
);

public interface IGroceryListRealtimeNotifier
{
	Task PublishListUpdatedAsync(
		Guid familyGroupId,
		DateOnly weekStart,
		GroceryList updatedList,
		string changedByUserId,
		GroceryListRealtimeEventType eventType,
		CancellationToken cancellationToken = default);
}

public sealed class GroceryListRealtimeNotifier(
	MealPlannerDbContext db,
	IHubContext<GroceryListHub> hubContext,
	ILogger<GroceryListRealtimeNotifier> logger)
	: IGroceryListRealtimeNotifier
{
	public async Task PublishListUpdatedAsync(
		Guid familyGroupId,
		DateOnly weekStart,
		GroceryList updatedList,
		string changedByUserId,
		GroceryListRealtimeEventType eventType,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var weekStartString = GroceryListHelpers.NormalizeToMonday(weekStart).ToString("yyyy-MM-dd");
			var memberIds = await db.FamilyGroupMembers
				.AsNoTracking()
				.Where(m => m.FamilyGroupId == familyGroupId)
				.Select(m => m.UserId)
				.ToListAsync(cancellationToken);

			var recipients = memberIds
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Distinct(StringComparer.Ordinal)
				.ToArray();

			if (recipients.Length == 0)
				return;

			var payload = new GroceryListUpdatedEvent(
				EventType: eventType.ToString(),
				FamilyGroupId: familyGroupId.ToString(),
				WeekStart: weekStartString,
				GroceryList: GroceryListResponse.FromDomain(updatedList),
				ChangedByUserId: changedByUserId,
				OccurredAt: DateTime.UtcNow);

			await hubContext.Clients
				.Users(recipients)
				.SendCoreAsync(GroceryListHub.GroceryListUpdatedMethod, [payload], cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex,
				"Failed to publish grocery list realtime update for family {FamilyGroupId} and week {WeekStart}",
				familyGroupId,
				weekStart.ToString("yyyy-MM-dd"));
		}
	}
}

public sealed class GroceryListUserIdProvider : IUserIdProvider
{
	public string? GetUserId(HubConnectionContext connection)
	{
		var user = connection.User;
		return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
		       ?? user.FindFirst("sub")?.Value;
	}
}
