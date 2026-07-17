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
	string FamilyGroupId,
	string WeekStart,
	MealPlanResponse MealPlan,
	string ChangedByUserId,
	DateTime OccurredAt
);

public interface IMealPlanRealtimeNotifier
{
	Task PublishMealPlanUpdatedAsync(
		Guid familyGroupId,
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
		Guid familyGroupId,
		DateOnly weekStart,
		MealPlan updatedPlan,
		string changedByUserId,
		MealPlanRealtimeEventType eventType,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var weekStartString = GetMealPlanQueryHandler.NormalizeToMonday(weekStart).ToString("yyyy-MM-dd");
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

			var payload = new MealPlanUpdatedEvent(
				EventType: eventType.ToString(),
				FamilyGroupId: familyGroupId.ToString(),
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
				"Failed to publish meal plan realtime update for family {FamilyGroupId} and week {WeekStart}",
				familyGroupId,
				weekStart.ToString("yyyy-MM-dd"));
		}
	}
}
