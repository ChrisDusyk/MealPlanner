import type { LayoutServerLoad } from './$types';
import { ApiError } from '$lib/api/apiHelpers';
import { getCurrentUser, syncCurrentUser, type AppUserResponse } from '$lib/api/userApi';

export const load: LayoutServerLoad = async (event) => {
	const session = await event.locals.auth();
	let appUser: AppUserResponse | null = null;

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
					appUser
				};
			}

			console.warn('Unable to load persisted app user profile.', error);
		}
	}

	return {
		session,
		appUser
	};
};
