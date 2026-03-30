using System.Security.Claims;
using MealPlanner.Api.Features.Auth;

namespace MealPlanner.Api.Tests.Features.Auth;

public class RbacAuthorizationTests
{
	[Fact]
	public void IsUserAuthorized_ReturnsTrue_WhenUserRolePresent()
	{
		var principal = CreatePrincipal("user");

		Assert.True(RbacAuthorization.IsUserAuthorized(principal));
	}

	[Fact]
	public void IsUserAuthorized_ReturnsTrue_WhenAdminRolePresent()
	{
		var principal = CreatePrincipal("admin");

		Assert.True(RbacAuthorization.IsUserAuthorized(principal));
	}

	[Fact]
	public void IsAdminAuthorized_ReturnsFalse_WhenOnlyUserRolePresent()
	{
		var principal = CreatePrincipal("user");

		Assert.False(RbacAuthorization.IsAdminAuthorized(principal));
	}

	[Fact]
	public void IsAdminAuthorized_ReturnsTrue_WhenAdminRolePresent()
	{
		var principal = CreatePrincipal("admin");

		Assert.True(RbacAuthorization.IsAdminAuthorized(principal));
	}

	[Fact]
	public void ExtractRoles_ParsesJsonArrayRoleClaim()
	{
		var principal = CreatePrincipal("[\"user\",\"admin\"]");

		var roles = RbacAuthorization.ExtractRoles(principal);

		Assert.Contains("user", roles);
		Assert.Contains("admin", roles);
	}

	private static ClaimsPrincipal CreatePrincipal(string roleClaimValue)
	{
		var identity = new ClaimsIdentity(
		[
			new Claim(RbacAuthorization.RoleClaimType, roleClaimValue),
			new Claim(ClaimTypes.NameIdentifier, "auth0|test-user")
		],
		"Test");

		return new ClaimsPrincipal(identity);
	}
}
