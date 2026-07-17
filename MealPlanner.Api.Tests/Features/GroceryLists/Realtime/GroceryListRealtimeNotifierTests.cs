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
	public async Task PublishListUpdatedAsync_SendsUpdateToAllFamilyMembers()
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

		var hubContext = new Mock<IHubContext<GroceryListHub>>();
		hubContext.SetupGet(h => h.Clients).Returns(hubClients.Object);

		var logger = new Mock<ILogger<GroceryListRealtimeNotifier>>();
		var notifier = new GroceryListRealtimeNotifier(context, hubContext.Object, logger.Object);

		var updatedList = new GroceryList(
			Id: "list-1",
			FamilyGroupId: familyId.ToString(),
			WeekStart: new DateOnly(2026, 2, 23),
			Items: [new GroceryListItem("Milk", 1, "L", false, [])],
			PantryStapleItems: [],
			CreatedAt: DateTime.UtcNow,
			UpdatedAt: DateTime.UtcNow);

		await notifier.PublishListUpdatedAsync(
			familyGroupId: familyId,
			weekStart: new DateOnly(2026, 2, 23),
			updatedList: updatedList,
			changedByUserId: "member-1",
			eventType: GroceryListRealtimeEventType.ItemToggled,
			cancellationToken: TestContext.Current.CancellationToken);

		Assert.NotNull(recipientIds);
		Assert.Equal(3, recipientIds!.Count);
		Assert.Contains("owner-1", recipientIds);
		Assert.Contains("member-1", recipientIds);
		Assert.Contains("member-2", recipientIds);

		clientProxy.Verify(c => c.SendCoreAsync(
				GroceryListHub.GroceryListUpdatedMethod,
				It.Is<object?[]>(args => args.Length == 1 && args[0] is GroceryListUpdatedEvent),
				It.IsAny<CancellationToken>()),
			Times.Once);
	}
}
