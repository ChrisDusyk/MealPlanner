using System.Text;
using System.Text.Json;
using MealPlanner.Api.Features.Integrations.GoogleKeep.Services;

namespace MealPlanner.Api.Tests.Features.Integrations.GoogleKeep.Services;

public class GoogleOAuthServiceTests
{
	[Fact]
	public void ParseIdTokenClaims_ReturnsSubjectAndEmail_WhenClaimsExist()
	{
		var payload = JsonSerializer.Serialize(new { sub = "google-subject-1", email = "user@example.com" });
		var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
			.TrimEnd('=')
			.Replace('+', '-')
			.Replace('/', '_');
		var token = $"header.{payloadBase64}.signature";

		var (subject, email) = GoogleOAuthService.ParseIdTokenClaims(token);

		Assert.True(subject.HasValue);
		Assert.Equal("google-subject-1", subject.Value);
		Assert.True(email.HasValue);
		Assert.Equal("user@example.com", email.Value);
	}

	[Fact]
	public void ParseIdTokenClaims_ReturnsNone_WhenTokenMalformed()
	{
		var (subject, email) = GoogleOAuthService.ParseIdTokenClaims("not-a-jwt");

		Assert.False(subject.HasValue);
		Assert.False(email.HasValue);
	}
}
