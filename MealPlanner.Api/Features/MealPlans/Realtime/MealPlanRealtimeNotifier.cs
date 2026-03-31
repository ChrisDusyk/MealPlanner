using MealPlanner.Api.Data;
using MealPlanner.Api.Features.MealPlans.Dtos;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Queries;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.MealPlans.Realtime;

public enum MealPlanRealtimeEventType
{
	DaySlotUpdated,
	CategoryCopied,
	SlotItemRemoved
}

public record MealPlanUpdatedEvent(
	string EventType,
	string OwnerUserId,
	string WeekStart,
	MealPlanResponse MealPlan,
	string ChangedByUserId,
	DateTime OccurredAt
);

public interface IMealPlanRealtimeNotifier
{
	Task PublishMealPlanUpdatedAsync(
		string ownerUserId,
		DateOnly weekStart,
		MealPlan updatedPlan,
		string changedByUserId,
		MealPlanRealtimeEventType eventType,
		CancellationToken cancellationToken = default);
}

public sealed class MealPlanRealtimeNotifier(
	MealPlannerDbContext db,
	IHubContext<MealPlanHub> hubContext,
	ILogger<MealPlanRealtimeNotifier> logger)
	: IMealPlanRealtimeNotifier
{
	public async Task PublishMealPlanUpdatedAsync(
		string ownerUserId,
		DateOnly weekStart,
		MealPlan updatedPlan,
		string changedByUserId,
		MealPlanRealtimeEventType eventType,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var weekStartString = GetMealPlanQueryHandler.NormalizeToMonday(weekStart).ToString("yyyy-MM-dd");
			var shares = await db.MealPlanShares
				.Where(s => s.OwnerUserId == ownerUserId
					&& s.WeekStart == weekStartString
					&& !s.DismissedByRecipient)
				.ToListAsync(cancellationToken);
			var recipients = shares.Select(s => s.SharedWithUserId)
				.Append(ownerUserId)
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Distinct(StringComparer.Ordinal)
				.ToArray();

			if (recipients.Length == 0)
				return;

			var payload = new MealPlanUpdatedEvent(
				EventType: eventType.ToString(),
				OwnerUserId: ownerUserId,
				WeekStart: weekStartString,
				MealPlan: MealPlanResponse.FromDomain(updatedPlan),
				ChangedByUserId: changedByUserId,
				OccurredAt: DateTime.UtcNow);

			await hubContext.Clients
				.Users(recipients)
				.SendCoreAsync(MealPlanHub.MealPlanUpdatedMethod, [payload], cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex,
				"Failed to publish meal plan realtime update for owner {OwnerUserId} and week {WeekStart}",
				ownerUserId,
				weekStart.ToString("yyyy-MM-dd"));
		}
	}
}
