using MealPlanner.Api.Features.MealPlans.Models;
using MealPlanner.Api.Features.MealPlans.Realtime;
using MealPlanner.Api.Shared;
using MealPlanner.Api.Tests.TestUtilities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.MealPlans.Realtime;

public class MealPlanRealtimeNotifierTests
{
	[Fact]
	public async Task PublishMealPlanUpdatedAsync_SendsUpdateToOwnerAndActiveRecipients()
	{
		var shares = new List<MealPlanShareDocument>
		{
			new()
			{
				OwnerUserId = "owner-1",
				SharedWithUserId = "guest-1",
				WeekStart = "2026-02-23",
				DismissedByRecipient = false
			},
			new()
			{
				OwnerUserId = "owner-1",
				SharedWithUserId = "guest-2",
				WeekStart = "2026-02-23",
				DismissedByRecipient = false
			}
		};

		var sharesCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<MealPlanShareDocument>)shares);
		var sharesCollection = new Mock<IMongoCollection<MealPlanShareDocument>>();
		sharesCollection
			.Setup(c => c.FindAsync(
				It.IsAny<FilterDefinition<MealPlanShareDocument>>(),
				It.IsAny<FindOptions<MealPlanShareDocument, MealPlanShareDocument>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(sharesCursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<MealPlanShareDocument>("shares", null))
			.Returns(sharesCollection.Object);

		var mongoClient = new Mock<IMongoClient>();
		mongoClient.Setup(c => c.GetDatabase("mealplannerDb", null)).Returns(db.Object);

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
		var notifier = new MealPlanRealtimeNotifier(mongoClient.Object, hubContext.Object, logger.Object);

		var updatedPlan = new MealPlan(
			Id: "plan-1",
			UserId: "owner-1",
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
			ownerUserId: "owner-1",
			weekStart: new DateOnly(2026, 2, 23),
			updatedPlan: updatedPlan,
			changedByUserId: "guest-1",
			eventType: MealPlanRealtimeEventType.DaySlotUpdated,
			cancellationToken: TestContext.Current.CancellationToken);

		Assert.NotNull(recipientIds);
		Assert.Equal(3, recipientIds!.Count);
		Assert.Contains("owner-1", recipientIds);
		Assert.Contains("guest-1", recipientIds);
		Assert.Contains("guest-2", recipientIds);

		clientProxy.Verify(c => c.SendCoreAsync(
				MealPlanHub.MealPlanUpdatedMethod,
				It.Is<object?[]>(args => args.Length == 1 && args[0] is MealPlanUpdatedEvent),
				It.IsAny<CancellationToken>()),
			Times.Once);
	}
}
