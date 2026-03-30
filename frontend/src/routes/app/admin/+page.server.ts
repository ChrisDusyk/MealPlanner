import type { ServerLoad } from '@sveltejs/kit';
import { requireRole } from '$lib/auth/guards';
import { APP_ROLES } from '$lib/auth/roles';

export const load: ServerLoad = async ({ locals }) => {
	const session = await locals.auth();
	requireRole(session, APP_ROLES.admin);

	return {};
};
