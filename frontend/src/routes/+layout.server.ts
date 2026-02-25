import type { LayoutServerLoad } from './$types';
import { getCurrentUser, type AppUserResponse } from '$lib/api/userApi';

export const load: LayoutServerLoad = async (event) => {
	const session = await event.locals.auth();
	let appUser: AppUserResponse | null = null;

	if (session?.accessToken) {
		try {
			appUser = await getCurrentUser(session.accessToken, event.fetch);
		} catch (error) {
			console.warn('Unable to load persisted app user profile.', error);
		}
	}

	return {
		session,
		appUser
	};
};
