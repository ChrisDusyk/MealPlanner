using MealPlanner.Api.Features.MealPlans.Models;

namespace MealPlanner.Api.Tests.Features.MealPlans.Models;

public class MealPlanShareDocumentTests
{
	[Fact]
	public void ToDomain_MapsDocumentToDomain()
	{
		var doc = new MealPlanShareDocument
		{
			Id = "s1",
			OwnerUserId = "owner1",
			SharedWithUserId = "recipient1",
			WeekStart = "2026-02-23",
			Permission = nameof(SharePermission.ReadOnly),
			SharedAt = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
			DismissedByRecipient = true
		};

		var domain = doc.ToDomain();

		Assert.Equal("s1", domain.Id);
		Assert.Equal(SharePermission.ReadOnly, domain.Permission);
		Assert.True(domain.DismissedByRecipient);
	}

	[Fact]
	public void FromDomain_MapsDomainToDocument_AndHandlesEmptyId()
	{
		var share = new MealPlanShare(
			string.Empty,
			"owner1",
			"recipient1",
			"2026-02-23",
			SharePermission.ReadWrite,
			new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
			false);

		var doc = MealPlanShareDocument.FromDomain(share);

		Assert.Null(doc.Id);
		Assert.Equal("owner1", doc.OwnerUserId);
		Assert.Equal("ReadWrite", doc.Permission);
		Assert.False(doc.DismissedByRecipient);
	}
}
