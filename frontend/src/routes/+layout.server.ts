import type { LayoutServerLoad } from './$types';
import { ApiError } from '$lib/api/apiHelpers';
import { getCurrentUser, syncCurrentUser, type AppUserResponse } from '$lib/api/userApi';
import { getServerFlags } from '$lib/server/featureFlags';
import type { AppSession } from '$lib/auth/session';
import type { EvaluationContext } from '@openfeature/server-sdk';

/**
 * Builds the OpenFeature context from the session. Attributes are omitted rather
 * than sent as empty strings so a targeting rule never matches a signed-out
 * visitor by accident.
 */
function buildEvaluationContext(session: AppSession | null): EvaluationContext | undefined {
	if (!session?.user?.id) {
		return undefined;
	}

	const context: EvaluationContext = { targetingKey: session.user.id };

	if (session.user.email) {
		context.email = session.user.email;
	}

	const role = session.roles?.[0];
	if (role) {
		context.role = role;
	}

	return context;
}

export const load: LayoutServerLoad = async (event) => {
	const session = await event.locals.auth();
	let appUser: AppUserResponse | null = null;

	// Feature flags are resolved server-side and handed to the browser via page
	// data. The context carries every attribute the admin targeting editor can
	// match on, so a rule authored there resolves the same way here.
	const flags = await getServerFlags(buildEvaluationContext(session));

	if (session?.accessToken) {
		try {
			appUser = await getCurrentUser(session.accessToken, event.fetch);
		} catch (error) {
			if (error instanceof ApiError && error.status === 404) {
				try {
					appUser = await syncCurrentUser(
						session.accessToken,
						{
							name: session.user?.name ?? null,
							email: session.user?.email ?? null
						},
						event.fetch
					);
				} catch (syncError) {
					console.warn('Unable to synchronize app user profile after 404 lookup.', syncError);
				}
				return {
					session,
					appUser,
					flags
				};
			}

			console.warn('Unable to load persisted app user profile.', error);
		}
	}

	return {
		session,
		appUser,
		flags
	};
};
