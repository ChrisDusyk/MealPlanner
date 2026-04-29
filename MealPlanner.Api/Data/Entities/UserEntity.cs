namespace MealPlanner.Api.Data.Entities;

/// <summary>
/// EF Core entity for application users.
/// </summary>
public class UserEntity
{
	public Guid Id { get; set; }

	public string AuthUserId { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string? Email { get; set; }

	/// <summary>
	/// Optional profile-display name captured during onboarding or settings.
	/// Falls back to <see cref="Name"/> in most UI flows when absent.
	/// </summary>
	public string? DisplayName { get; set; }

	/// <summary>
	/// IANA timezone identifier (e.g. <c>America/Toronto</c>) used for scheduling
	/// meal plans and grocery lists. Null until the user completes onboarding.
	/// </summary>
	public string? Timezone { get; set; }

	/// <summary>
	/// Set when the user finishes the first-time onboarding wizard. Null means
	/// onboarding is still pending for this account.
	/// </summary>
	public DateTime? OnboardingCompletedAt { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}
