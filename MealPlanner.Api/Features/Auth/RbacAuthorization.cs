using System.Security.Claims;
using System.Text.Json;

namespace MealPlanner.Api.Features.Auth;

public static class RbacAuthorization
{
	/// <summary>
	/// Legacy namespaced role claim preserved for backwards compatibility with
	/// existing tokens and external consumers. Better Auth's JWT plugin emits
	/// this alongside the modern <see cref="NativeRoleClaimType"/> claim.
	/// </summary>
	public const string RoleClaimType = "https://mealplanner/roles";

	/// <summary>
	/// Standard role claim emitted by Better Auth's admin plugin. The API
	/// accepts either this value or <see cref="RoleClaimType"/>, letting us
	/// retire the legacy claim in a future release without a breaking change.
	/// </summary>
	public const string NativeRoleClaimType = "role";

	public const string UserRole = "user";
	public const string AdminRole = "admin";

	public const string RequireUserRolePolicy = "RequireUserRole";
	public const string RequireAdminRolePolicy = "RequireAdminRole";

	public static bool IsUserAuthorized(ClaimsPrincipal user) =>
		HasAnyRole(user, UserRole, AdminRole);

	public static bool IsAdminAuthorized(ClaimsPrincipal user) =>
		HasAnyRole(user, AdminRole);

	public static bool HasAnyRole(ClaimsPrincipal user, params string[] requiredRoles)
	{
		if (requiredRoles.Length == 0)
		{
			return false;
		}

		var userRoles = ExtractRoles(user).ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (userRoles.Count == 0)
		{
			return false;
		}

		return requiredRoles.Any(role => userRoles.Contains(role));
	}

	public static IReadOnlyList<string> ExtractRoles(ClaimsPrincipal user)
	{
		var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		// Accept the legacy namespaced claim, the modern `role` claim emitted by
		// Better Auth's admin plugin, and the canonical .NET role claim so that
		// existing tokens and new tokens are both understood.
		foreach (var claimType in new[] { RoleClaimType, NativeRoleClaimType, ClaimTypes.Role })
		{
			foreach (var claim in user.FindAll(claimType))
			{
				foreach (var role in ParseRoleClaimValue(claim.Value))
				{
					roles.Add(role);
				}
			}
		}

		return roles.ToList();
	}

	private static IEnumerable<string> ParseRoleClaimValue(string rawValue)
	{
		if (string.IsNullOrWhiteSpace(rawValue))
		{
			yield break;
		}

		var value = rawValue.Trim();

		if (TryParseJsonRoles(value, out var jsonRoles))
		{
			foreach (var role in jsonRoles)
			{
				yield return role;
			}

			yield break;
		}

		if (value.Contains(','))
		{
			foreach (var role in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				yield return role;
			}

			yield break;
		}

		yield return value;
	}

	private static bool TryParseJsonRoles(string value, out IReadOnlyList<string> roles)
	{
		roles = [];
		if (!(value.StartsWith('[') || value.StartsWith('"')))
		{
			return false;
		}

		try
		{
			using var document = JsonDocument.Parse(value);
			switch (document.RootElement.ValueKind)
			{
				case JsonValueKind.Array:
				{
					var parsed = new List<string>();
					foreach (var element in document.RootElement.EnumerateArray())
					{
						if (element.ValueKind == JsonValueKind.String)
						{
							var role = element.GetString();
							if (!string.IsNullOrWhiteSpace(role))
							{
								parsed.Add(role.Trim());
							}
						}
					}

					roles = parsed;
					return true;
				}
				case JsonValueKind.String:
				{
					var role = document.RootElement.GetString();
					if (string.IsNullOrWhiteSpace(role))
					{
						return true;
					}

					roles = [role.Trim()];
					return true;
				}
				default:
					return false;
			}
		}
		catch (JsonException)
		{
			return false;
		}
	}
}
