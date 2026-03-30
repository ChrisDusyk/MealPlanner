using System.Security.Claims;
using System.Text.Json;

namespace MealPlanner.Api.Features.Auth;

public static class RbacAuthorization
{
	public const string RoleClaimType = "https://mealplanner/roles";
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
		var roleClaims = user.FindAll(RoleClaimType);

		foreach (var claim in roleClaims)
		{
			foreach (var role in ParseRoleClaimValue(claim.Value))
			{
				roles.Add(role);
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
