import { redirect } from '@sveltejs/kit';
import type { LayoutServerLoad } from './$types';
import { requireAuthenticatedSession } from '$lib/auth/guards';
import { getIncomingFamilyInvitations, type FamilyInvitationResponse } from '$lib/api/familyApi';

export const load: LayoutServerLoad = async (event) => {
	const rawSession = await event.locals.auth();
	const session = requireAuthenticatedSession(rawSession);

	// The root layout already synchronises the app-side user record and exposes
	// it via `parent().appUser`. Consulting that here keeps us from making a
	// second round-trip while still enforcing the onboarding gate for every
	// authenticated page in /app (except /app/onboarding itself).
	const parent = await event.parent();
	const appUser = parent.appUser;
	const pathname = event.url.pathname;
	const isOnboardingRoute = pathname === '/app/onboarding' || pathname.startsWith('/app/onboarding/');

	if (appUser && !appUser.onboardingCompletedAt && !isOnboardingRoute) {
		throw redirect(303, '/app/onboarding');
	}

	// Pending family invitations surface as a banner on every app page.
	// Failures are non-fatal — the app works without the banner.
	let familyInvitations: FamilyInvitationResponse[] = [];
	if (session.accessToken) {
		try {
			familyInvitations = await getIncomingFamilyInvitations(session.accessToken, event.fetch);
		} catch (err) {
			console.error('Failed to load family invitations:', err);
		}
	}

	return {
		session,
		familyInvitations
	};
};
