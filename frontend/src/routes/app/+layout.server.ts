import type { LayoutServerLoad } from './$types';
import { requireAuthenticatedSession } from '$lib/auth/guards';

export const load: LayoutServerLoad = async (event) => {
	const rawSession = await event.locals.auth();
	const session = requireAuthenticatedSession(rawSession);

	return {
		session
	};
};
