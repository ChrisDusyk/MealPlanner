import { error } from '@sveltejs/kit';
import { adminListCatalogueTags } from '$lib/api/adminCatalogueApi';
import { requireRole } from '$lib/auth/guards';
import { APP_ROLES } from '$lib/auth/roles';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, locals, fetch }) => {
	const { session } = await parent();
	requireRole(await locals.auth(), APP_ROLES.admin);

	try {
		const tags = await adminListCatalogueTags(session.accessToken, fetch);
		return { tags };
	} catch (err) {
		console.error('Failed to load tags:', err);
		throw error(500, 'Failed to load tags.');
	}
};
