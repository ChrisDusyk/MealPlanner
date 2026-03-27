using MealPlanner.Api.Features.Integrations.GoogleKeep.Queries;
using MealPlanner.Api.Shared;
using Moq;
using MongoDB.Driver;

namespace MealPlanner.Api.Tests.Features.Integrations.GoogleKeep.Queries;

public class GetGoogleKeepConnectionStatusTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenUserIdMissing()
	{
		var handler = new GetGoogleKeepConnectionStatusQueryHandler(new Mock<IMongoClient>().Object);
		var result = await handler.HandleAsync(new GetGoogleKeepConnectionStatusQuery(" "), TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
	}
}
