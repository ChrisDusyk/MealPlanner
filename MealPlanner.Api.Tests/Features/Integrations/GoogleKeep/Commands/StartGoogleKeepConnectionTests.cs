using MealPlanner.Api.Features.Integrations.GoogleKeep.Commands;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Services;
using MealPlanner.Api.Shared;
using Moq;

namespace MealPlanner.Api.Tests.Features.Integrations.GoogleKeep.Commands;

public class StartGoogleKeepConnectionTests
{
	[Fact]
	public async Task HandleAsync_ReturnsValidationFailure_WhenUserIdMissing()
	{
		var oauth = new Mock<IGoogleOAuthService>();
		var handler = new StartGoogleKeepConnectionCommandHandler(oauth.Object);

		var result = await handler.HandleAsync(
			new StartGoogleKeepConnectionCommand(" ", "https://app.example.com/callback"),
			TestContext.Current.CancellationToken);

		Assert.False(result.IsSuccess);
		Assert.NotNull(result.Error);
		Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
	}

	[Fact]
	public async Task HandleAsync_ReturnsAuthUrlAndState_WhenOAuthServiceSucceeds()
	{
		var oauth = new Mock<IGoogleOAuthService>();
		oauth.Setup(x => x.BuildAuthorizationUrlAsync(
				"auth0|user1",
				"https://app.example.com/callback",
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(Result<GoogleAuthorizationStart>.Success(
				new GoogleAuthorizationStart("https://accounts.google.com/o/oauth2/v2/auth?...", "state123")));
		var handler = new StartGoogleKeepConnectionCommandHandler(oauth.Object);

		var result = await handler.HandleAsync(
			new StartGoogleKeepConnectionCommand("auth0|user1", "https://app.example.com/callback"),
			TestContext.Current.CancellationToken);

		Assert.True(result.IsSuccess);
		Assert.NotNull(result.Value);
		Assert.Equal("state123", result.Value.State);
		Assert.StartsWith("https://accounts.google.com/", result.Value.AuthorizationUrl, StringComparison.Ordinal);
	}
}
