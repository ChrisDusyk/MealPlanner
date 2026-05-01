import { error } from '@sveltejs/kit';
import { adminListCatalogue, adminListCatalogueTags } from '$lib/api/adminCatalogueApi';
import { requireRole } from '$lib/auth/guards';
import { APP_ROLES } from '$lib/auth/roles';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, locals, fetch, url }) => {
	const { session } = await parent();
	requireRole(await locals.auth(), APP_ROLES.admin);

	const search = url.searchParams.get('search') ?? '';
	const isPublishedParam = url.searchParams.get('isPublished');
	const isPublished =
		isPublishedParam === 'true' ? true : isPublishedParam === 'false' ? false : null;

	try {
		const [recipes, tags] = await Promise.all([
			adminListCatalogue(session.accessToken, { search: search || undefined, isPublished }, fetch),
			adminListCatalogueTags(session.accessToken, fetch)
		]);
		return { recipes, tags, search, isPublished };
	} catch (err) {
		console.error('Failed to load admin catalogue:', err);
		throw error(500, 'Failed to load catalogue.');
	}
};
