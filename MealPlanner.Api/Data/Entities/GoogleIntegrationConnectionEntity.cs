namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for a Google integration connection.
/// </summary>
public class GoogleIntegrationConnectionEntity
{
	public Guid Id { get; set; }

	public string UserId { get; set; } = string.Empty;

	public string GoogleSubject { get; set; } = string.Empty;

	public string Provider { get; set; } = string.Empty;

	public string? GoogleEmail { get; set; }

	public string EncryptedAccessToken { get; set; } = string.Empty;

	public string? EncryptedRefreshToken { get; set; }

	public DateTime? AccessTokenExpiresAtUtc { get; set; }

	/// <summary>
	/// Stored as jsonb in PostgreSQL.
	/// </summary>
	public List<string> Scopes { get; set; } = [];

	public string Capability { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }

	public DateTime? DisconnectedAtUtc { get; set; }
}
