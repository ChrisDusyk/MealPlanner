using System.Security.Cryptography;
using System.Text;

namespace MealPlanner.Api.Tests.TestUtilities;

/// <summary>
/// Deterministic ids for tests. Family ids are derived from a stable key so
/// tests can refer to "the same family" without threading Guid variables
/// through every seed block.
/// </summary>
public static class TestIds
{
	public static Guid Family(string key)
	{
		var hash = MD5.HashData(Encoding.UTF8.GetBytes("family:" + key));
		return new Guid(hash);
	}
}
