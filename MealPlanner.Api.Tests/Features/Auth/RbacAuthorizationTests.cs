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

	[Fact]
	public void ExtractRoles_AcceptsNativeBetterAuthRoleClaim()
	{
		// Better Auth's admin plugin emits the standard unprefixed `role` claim;
		// the API must accept it going forward so we can retire the legacy
		// namespaced claim without impacting clients.
		var identity = new ClaimsIdentity(
		[
			new Claim(RbacAuthorization.NativeRoleClaimType, "admin"),
			new Claim(ClaimTypes.NameIdentifier, "better-auth|test-user")
		], "Test");
		var principal = new ClaimsPrincipal(identity);

		Assert.True(RbacAuthorization.IsAdminAuthorized(principal));
		Assert.Contains("admin", RbacAuthorization.ExtractRoles(principal));
	}

	[Fact]
	public void ExtractRoles_AcceptsClaimTypesRoleClaim()
	{
		// AddJwtBearer may map incoming `role` JWT claims into the canonical
		// System.Security.Claims.ClaimTypes.Role via its configured role claim
		// type; this test guards that path explicitly.
		var identity = new ClaimsIdentity(
		[
			new Claim(ClaimTypes.Role, "user"),
			new Claim(ClaimTypes.NameIdentifier, "better-auth|test-user")
		], "Test");
		var principal = new ClaimsPrincipal(identity);

		Assert.True(RbacAuthorization.IsUserAuthorized(principal));
	}

	[Fact]
	public void ExtractRoles_MergesClaimsFromMultipleSources()
	{
		var identity = new ClaimsIdentity(
		[
			new Claim(RbacAuthorization.RoleClaimType, "user"),
			new Claim(RbacAuthorization.NativeRoleClaimType, "admin"),
			new Claim(ClaimTypes.NameIdentifier, "better-auth|test-user")
		], "Test");
		var principal = new ClaimsPrincipal(identity);

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
