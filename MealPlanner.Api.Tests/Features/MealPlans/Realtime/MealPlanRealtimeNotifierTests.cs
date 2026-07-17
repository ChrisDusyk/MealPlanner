using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Realtime;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MealPlanner.Api.Tests.Features.MealPlans.Realtime;

public class MealPlanRealtimeNotifierTests
{
	[Fact]
	public async Task PublishMealPlanUpdatedAsync_SendsUpdateToAllFamilyMembers()
	{
		var familyId = TestIds.Family("fam-1");
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.FamilyGroups.Add(new FamilyGroupEntity
			{
				Id = familyId,
				Name = "Fam",
				OwnerUserId = "owner-1",
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});
			db.FamilyGroupMembers.AddRange(
				new FamilyGroupMemberEntity
				{
					Id = Guid.NewGuid(),
					FamilyGroupId = familyId,
					UserId = "owner-1",
					JoinedAt = DateTime.UtcNow
				},
				new FamilyGroupMemberEntity
				{
					Id = Guid.NewGuid(),
					FamilyGroupId = familyId,
					UserId = "member-1",
					JoinedAt = DateTime.UtcNow
				},
				new FamilyGroupMemberEntity
				{
					Id = Guid.NewGuid(),
					FamilyGroupId = familyId,
					UserId = "member-2",
					JoinedAt = DateTime.UtcNow
				});
		});

		var clientProxy = new Mock<IClientProxy>();
		IReadOnlyList<string>? recipientIds = null;

		var hubClients = new Mock<IHubClients>();
		hubClients
			.Setup(c => c.Users(It.IsAny<IReadOnlyList<string>>()))
			.Callback<IReadOnlyList<string>>(ids => recipientIds = ids)
			.Returns(clientProxy.Object);

		var hubContext = new Mock<IHubContext<MealPlanHub>>();
		hubContext.SetupGet(h => h.Clients).Returns(hubClients.Object);

		var logger = new Mock<ILogger<MealPlanRealtimeNotifier>>();
		var notifier = new MealPlanRealtimeNotifier(context, hubContext.Object, logger.Object);

		var updatedPlan = new MealPlan(
			Id: "plan-1",
			FamilyGroupId: familyId.ToString(),
			WeekStart: new DateOnly(2026, 2, 23),
			Days:
			[
				new DayPlan(
					Day: DayOfWeek.Monday,
					Slots: new Dictionary<MealCategory, List<MealSlotItem>>
					{
						{ MealCategory.Breakfast, [new MealSlotItem(Option<string>.None(), "Eggs")] },
						{ MealCategory.Lunch, [] },
						{ MealCategory.Supper, [] },
						{ MealCategory.Snacks, [] }
					})
			],
			CreatedAt: DateTime.UtcNow,
			UpdatedAt: DateTime.UtcNow);

		await notifier.PublishMealPlanUpdatedAsync(
			familyGroupId: familyId,
			weekStart: new DateOnly(2026, 2, 23),
			updatedPlan: updatedPlan,
			changedByUserId: "member-1",
			eventType: MealPlanRealtimeEventType.DaySlotUpdated,
			cancellationToken: TestContext.Current.CancellationToken);

		Assert.NotNull(recipientIds);
		Assert.Equal(3, recipientIds!.Count);
		Assert.Contains("owner-1", recipientIds);
		Assert.Contains("member-1", recipientIds);
		Assert.Contains("member-2", recipientIds);

		clientProxy.Verify(c => c.SendCoreAsync(
				MealPlanHub.MealPlanUpdatedMethod,
				It.Is<object?[]>(args => args.Length == 1 && args[0] is MealPlanUpdatedEvent),
				It.IsAny<CancellationToken>()),
			Times.Once);
	}
}
