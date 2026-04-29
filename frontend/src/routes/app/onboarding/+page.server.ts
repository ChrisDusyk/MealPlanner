import { fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { requireAuthenticatedSession } from '$lib/auth/guards';
import { completeOnboarding, syncCurrentUser } from '$lib/api/userApi';
import { ApiError } from '$lib/api/apiHelpers';

export const load: PageServerLoad = async (event) => {
	const rawSession = await event.locals.auth();
	const session = requireAuthenticatedSession(rawSession);
	const parent = await event.parent();

	// If the user has already completed onboarding, skip the wizard.
	if (parent.appUser?.onboardingCompletedAt) {
		throw redirect(303, '/app');
	}

	return {
		session,
		appUser: parent.appUser
	};
};

export const actions = {
	default: async (event) => {
		const rawSession = await event.locals.auth();
		const session = requireAuthenticatedSession(rawSession);
		const data = await event.request.formData();
		const displayName = (data.get('displayName')?.toString() ?? '').trim();
		const timezone = (data.get('timezone')?.toString() ?? '').trim();

		if (!session.accessToken) {
			return fail(401, {
				error: 'Your session has expired. Please sign in again.',
				displayName,
				timezone
			});
		}

		if (!displayName) {
			return fail(400, {
				error: 'Please tell us what to call you.',
				displayName,
				timezone
			});
		}

		try {
			const request = {
				displayName,
				timezone: timezone.length > 0 ? timezone : null
			};

			try {
				await completeOnboarding(session.accessToken, request, event.fetch);
			} catch (err) {
				// New signups can briefly hit a stale/mismatched app-user row.
				// Re-sync from auth claims and retry once before surfacing an error.
				if (!(err instanceof ApiError) || err.status !== 404) {
					throw err;
				}

				await syncCurrentUser(
					session.accessToken,
					{
						name: session.user?.name ?? null,
						email: session.user?.email ?? null
					},
					event.fetch
				);
				await completeOnboarding(session.accessToken, request, event.fetch);
			}
		} catch (err) {
			const message =
				err instanceof ApiError
					? err.message
					: 'We couldn\u2019t finish setting up your account. Please try again.';
			return fail(err instanceof ApiError ? err.status : 500, {
				error: message,
				displayName,
				timezone
			});
		}

		throw redirect(303, '/app');
	}
} satisfies Actions;
