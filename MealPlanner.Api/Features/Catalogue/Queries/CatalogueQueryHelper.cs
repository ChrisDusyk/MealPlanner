using MealPlanner.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Catalogue.Queries;

internal static class CatalogueQueryHelper
{
	private const string EscapeChar = @"\";

	/// <summary>
	/// Applies a case-insensitive full-text search predicate to <paramref name="q"/>.
	/// Uses <c>ILIKE</c> on the Npgsql provider (avoids <c>LOWER(column)</c> and preserves
	/// index-friendliness); falls back to <c>ToLower().Contains()</c> for other providers
	/// such as the EF Core InMemory provider used in unit tests.
	/// </summary>
	internal static IQueryable<CatalogueRecipeEntity> WhereMatchesSearch(
		this IQueryable<CatalogueRecipeEntity> q,
		string searchTerm,
		string? providerName)
	{
		if (providerName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
		{
			var escaped = searchTerm.Trim()
				.Replace(EscapeChar, EscapeChar + EscapeChar)
				.Replace("%", EscapeChar + "%")
				.Replace("_", EscapeChar + "_");
			var pattern = $"%{escaped}%";
			return q.Where(r =>
				EF.Functions.ILike(r.Name, pattern, EscapeChar) ||
				EF.Functions.ILike(r.Description, pattern, EscapeChar));
		}
		else
		{
			var search = searchTerm.Trim().ToLower();
			return q.Where(r =>
				r.Name.ToLower().Contains(search) ||
				r.Description.ToLower().Contains(search));
		}
	}
}
