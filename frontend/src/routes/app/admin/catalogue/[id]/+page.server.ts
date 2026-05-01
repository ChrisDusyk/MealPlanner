import { error } from '@sveltejs/kit';
import {
	adminGetCatalogueRecipe,
	adminListCatalogueTags
} from '$lib/api/adminCatalogueApi';
import { ApiError } from '$lib/api/apiHelpers';
import { requireRole } from '$lib/auth/guards';
import { APP_ROLES } from '$lib/auth/roles';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, locals, fetch, params }) => {
	const { session } = await parent();
	requireRole(await locals.auth(), APP_ROLES.admin);

	try {
		const [recipe, tags] = await Promise.all([
			adminGetCatalogueRecipe(session.accessToken, params.id, fetch),
			adminListCatalogueTags(session.accessToken, fetch)
		]);
		return { recipe, tags };
	} catch (err) {
		if (err instanceof ApiError && err.status === 404) {
			throw error(404, 'Catalogue recipe not found.');
		}
		console.error('Failed to load catalogue recipe:', err);
		throw error(500, 'Failed to load recipe.');
	}
};
