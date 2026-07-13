using Npgsql;

namespace MealPlanner.Api.Shared;

/// <summary>
/// Converts a URI-format database URL (as provided by Railway and similar hosts,
/// e.g. postgresql://user:pass@host:5432/db) into the ADO.NET key-value connection
/// string that Npgsql expects. Credentials in the URL are percent-encoded, so they
/// must be unescaped, and the password may itself contain ':'.
/// </summary>
internal static class DatabaseUrlParser
{
	internal static string ToConnectionString(string databaseUrl)
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

		return builder.ConnectionString;
	}
}
