using MealPlanner.Api.Features.MealPlans.Dtos;
using MealPlanner.Api.Features.MealPlans.Models;

namespace MealPlanner.Api.Tests.Features.MealPlans.Dtos;

public class ShareDtosTests
{
	[Fact]
	public void MealPlanShareResponse_FromDomain_MapsWithRecipientInfo()
	{
		var share = new MealPlanShare(
			"s1",
			"owner1",
			"recipient1",
			"2026-02-23",
			SharePermission.ReadWrite,
			new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
			false);

		var response = MealPlanShareResponse.FromDomain(share, "Alex", "alex@example.com");

		Assert.Equal("s1", response.Id);
		Assert.Equal("owner1", response.OwnerUserId);
		Assert.Equal("recipient1", response.SharedWithUserId);
		Assert.Equal("Alex", response.SharedWithName);
		Assert.Equal("alex@example.com", response.SharedWithEmail);
		Assert.Equal("ReadWrite", response.Permission);
	}
}
