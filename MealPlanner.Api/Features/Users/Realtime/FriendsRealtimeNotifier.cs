using Microsoft.AspNetCore.SignalR;

namespace MealPlanner.Api.Features.Users.Realtime;

public enum FriendsRealtimeEventType
{
	RequestSent,
	RequestAccepted,
	RequestRejected,
	FriendshipRemoved
}

public record FriendsUpdatedEvent(
	string EventType,
	string ChangedByUserId,
	DateTime OccurredAt
);

public interface IFriendsRealtimeNotifier
{
	Task PublishFriendsUpdatedAsync(
		IEnumerable<string> userIds,
		string changedByUserId,
		FriendsRealtimeEventType eventType,
		CancellationToken cancellationToken = default);
}

public sealed class FriendsRealtimeNotifier(
	IHubContext<FriendsHub> hubContext,
	ILogger<FriendsRealtimeNotifier> logger)
	: IFriendsRealtimeNotifier
{
	public async Task PublishFriendsUpdatedAsync(
		IEnumerable<string> userIds,
		string changedByUserId,
		FriendsRealtimeEventType eventType,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var recipients = userIds
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Distinct(StringComparer.Ordinal)
				.ToArray();

			if (recipients.Length == 0)
				return;

			var payload = new FriendsUpdatedEvent(
				EventType: eventType.ToString(),
				ChangedByUserId: changedByUserId,
				OccurredAt: DateTime.UtcNow);

			await hubContext.Clients
				.Users(recipients)
				.SendCoreAsync(FriendsHub.FriendsUpdatedMethod, [payload], cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogWarning(
				ex,
				"Failed to publish realtime friend update for user {ChangedByUserId}",
				changedByUserId);
		}
	}
}
