namespace MealPlanner.Api.Features.Users.Dtos;

/// <summary>
/// Payload submitted by the SvelteKit onboarding wizard to finalise
/// the user's profile and mark the onboarding flow as complete.
/// </summary>
public record CompleteOnboardingRequest(
	string? DisplayName,
	string? Timezone
);
