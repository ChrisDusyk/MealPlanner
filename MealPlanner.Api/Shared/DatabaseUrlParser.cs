using Npgsql;

namespace MealPlanner.Api.Shared;

/// <summary>
/// Converts a URI-format database URL (as provided by Railway and similar hosts,
/// e.g. postgresql://user:pass@host:5432/db?sslmode=require) into the ADO.NET
/// key-value connection string that Npgsql expects. Credentials in the URL are
/// percent-encoded, so they must be unescaped, and the password may itself
/// contain ':'.
/// </summary>
public static class DatabaseUrlParser
{
	public static string ToConnectionString(string databaseUrl)
	{
		var uri = new Uri(databaseUrl);
		var userInfo = uri.UserInfo.Split(':', 2);

		var builder = new NpgsqlConnectionStringBuilder
		{
			Host = uri.Host,
			Port = uri.Port > 0 ? uri.Port : 5432,
			Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
			Username = Uri.UnescapeDataString(userInfo[0])
		};

		if (userInfo.Length > 1)
			builder.Password = Uri.UnescapeDataString(userInfo[1]);

		// Hosts commonly append SSL requirements as query parameters; dropping
		// them would silently downgrade the connection security.
		foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
		{
			var parts = pair.Split('=', 2);
			var key = Uri.UnescapeDataString(parts[0]);
			var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;

			// URLs use libpq spellings like "verify-full"; the enum value is "VerifyFull".
			if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase)
			    && Enum.TryParse<SslMode>(value.Replace("-", ""), ignoreCase: true, out var sslMode))
			{
				builder.SslMode = sslMode;
			}
		}

		return builder.ConnectionString;
	}
}
