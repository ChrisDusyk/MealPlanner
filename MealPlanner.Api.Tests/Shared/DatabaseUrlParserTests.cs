using MealPlanner.Api.Shared;
using Npgsql;

namespace MealPlanner.Api.Tests.Shared;

public class DatabaseUrlParserTests
{
	[Fact]
	public void ToConnectionString_ParsesSimpleUrl()
	{
		var connectionString = DatabaseUrlParser.ToConnectionString(
			"postgresql://appuser:secret@db.example.com:5433/mealplanner");

		var parsed = new NpgsqlConnectionStringBuilder(connectionString);
		Assert.Equal("db.example.com", parsed.Host);
		Assert.Equal(5433, parsed.Port);
		Assert.Equal("mealplanner", parsed.Database);
		Assert.Equal("appuser", parsed.Username);
		Assert.Equal("secret", parsed.Password);
	}

	[Fact]
	public void ToConnectionString_DecodesPercentEncodedCredentials()
	{
		var connectionString = DatabaseUrlParser.ToConnectionString(
			"postgresql://app%40user:p%40ss%3Aw0rd%2F%2B%3B@db.example.com:5432/mealplanner");

		var parsed = new NpgsqlConnectionStringBuilder(connectionString);
		Assert.Equal("app@user", parsed.Username);
		Assert.Equal("p@ss:w0rd/+;", parsed.Password);
	}

	[Fact]
	public void ToConnectionString_KeepsPasswordWithUnencodedColon()
	{
		var connectionString = DatabaseUrlParser.ToConnectionString(
			"postgresql://appuser:pass:word@db.example.com:5432/mealplanner");

		var parsed = new NpgsqlConnectionStringBuilder(connectionString);
		Assert.Equal("appuser", parsed.Username);
		Assert.Equal("pass:word", parsed.Password);
	}

	[Fact]
	public void ToConnectionString_DefaultsPortTo5432_WhenMissing()
	{
		var connectionString = DatabaseUrlParser.ToConnectionString(
			"postgresql://appuser:secret@db.example.com/mealplanner");

		var parsed = new NpgsqlConnectionStringBuilder(connectionString);
		Assert.Equal(5432, parsed.Port);
	}

	[Fact]
	public void ToConnectionString_HandlesMissingPassword()
	{
		var connectionString = DatabaseUrlParser.ToConnectionString(
			"postgresql://appuser@db.example.com:5432/mealplanner");

		var parsed = new NpgsqlConnectionStringBuilder(connectionString);
		Assert.Equal("appuser", parsed.Username);
		Assert.True(string.IsNullOrEmpty(parsed.Password));
	}
}
