using MealPlanner.Api.Data.Entities;
using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.GroceryLists.Realtime;
using MealPlanner.Api.Tests.TestUtilities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Realtime;

public class GroceryListRealtimeNotifierTests
{
	[Fact]
	public async Task PublishListUpdatedAsync_SendsUpdateToOwnerAndActiveRecipients()
	{
		var context = TestDbContextFactory.CreateContext(seed: db =>
		{
			db.GroceryListShares.AddRange(
				new GroceryListShareEntity
				{
					Id = Guid.NewGuid(),
					OwnerUserId = "owner-1",
					SharedWithUserId = "guest-1",
					WeekStart = "2026-02-23",
					Permission = "ReadWrite",
					DismissedByRecipient = false,
					SharedAt = DateTime.UtcNow
				},
				new GroceryListShareEntity
				{
					Id = Guid.NewGuid(),
					OwnerUserId = "owner-1",
					SharedWithUserId = "guest-2",
					WeekStart = "2026-02-23",
					Permission = "ReadWrite",
					DismissedByRecipient = false,
					SharedAt = DateTime.UtcNow
				});
		});

		var clientProxy = new Mock<IClientProxy>();
		IReadOnlyList<string>? recipientIds = null;

		var hubClients = new Mock<IHubClients>();
		hubClients
			.Setup(c => c.Users(It.IsAny<IReadOnlyList<string>>()))
			.Callback<IReadOnlyList<string>>(ids => recipientIds = ids)
			.Returns(clientProxy.Object);

		var hubContext = new Mock<IHubContext<GroceryListHub>>();
		hubContext.SetupGet(h => h.Clients).Returns(hubClients.Object);

		var logger = new Mock<ILogger<GroceryListRealtimeNotifier>>();
		var notifier = new GroceryListRealtimeNotifier(context, hubContext.Object, logger.Object);

		var updatedList = new GroceryList(
			Id: "list-1",
			UserId: "owner-1",
			WeekStart: new DateOnly(2026, 2, 23),
			Items:
			[
				new GroceryListItem(
					Name: "Milk",
					Quantity: 1,
					Unit: "L",
					IsChecked: true,
					SourceRecipeNames: [])
			],
			PantryStapleItems: [],
			CreatedAt: DateTime.UtcNow,
			UpdatedAt: DateTime.UtcNow);

		await notifier.PublishListUpdatedAsync(
			ownerUserId: "owner-1",
			weekStart: new DateOnly(2026, 2, 23),
			updatedList: updatedList,
			changedByUserId: "guest-1",
			eventType: GroceryListRealtimeEventType.ItemToggled,
			cancellationToken: TestContext.Current.CancellationToken);

		Assert.NotNull(recipientIds);
		Assert.Equal(3, recipientIds!.Count);
		Assert.Contains("owner-1", recipientIds);
		Assert.Contains("guest-1", recipientIds);
		Assert.Contains("guest-2", recipientIds);

		clientProxy.Verify(c => c.SendCoreAsync(
				GroceryListHub.GroceryListUpdatedMethod,
				It.Is<object?[]>(args => args.Length == 1 && args[0] is GroceryListUpdatedEvent),
				It.IsAny<CancellationToken>()),
			Times.Once);
	}
}
