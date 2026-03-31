using MealPlanner.Api.Features.GroceryLists.Models;
using MealPlanner.Api.Features.GroceryLists.Realtime;
using MealPlanner.Api.Tests.TestUtilities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.GroceryLists.Realtime;

public class GroceryListRealtimeNotifierTests
{
	[Fact]
	public async Task PublishListUpdatedAsync_SendsUpdateToOwnerAndActiveRecipients()
	{
		var shares = new List<GroceryListShareDocument>
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

		var sharesCursor = MongoTestHelpers.CreateCursor((IReadOnlyCollection<GroceryListShareDocument>)shares);
		var sharesCollection = new Mock<IMongoCollection<GroceryListShareDocument>>();
		sharesCollection
			.Setup(c => c.FindAsync(
				It.IsAny<FilterDefinition<GroceryListShareDocument>>(),
				It.IsAny<FindOptions<GroceryListShareDocument, GroceryListShareDocument>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(sharesCursor.Object);

		var db = new Mock<IMongoDatabase>();
		db.Setup(d => d.GetCollection<GroceryListShareDocument>("grocerylist_shares", null))
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

		var hubContext = new Mock<IHubContext<GroceryListHub>>();
		hubContext.SetupGet(h => h.Clients).Returns(hubClients.Object);

		var logger = new Mock<ILogger<GroceryListRealtimeNotifier>>();
		var notifier = new GroceryListRealtimeNotifier(TestDbContextFactory.CreateContext(), hubContext.Object, logger.Object);

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
