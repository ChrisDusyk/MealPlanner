import { error } from '@sveltejs/kit';
import { ApiError } from '$lib/api/apiHelpers';
import { getFeatureFlag } from '$lib/api/featureFlagsApi';
import { requireRole } from '$lib/auth/guards';
import { APP_ROLES } from '$lib/auth/roles';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, params, locals, fetch }) => {
	const { session } = await parent();
	requireRole(await locals.auth(), APP_ROLES.admin);

	try {
		const flag = await getFeatureFlag(session.accessToken, params.key, fetch);
		return { flag };
	} catch (err) {
		if (err instanceof ApiError && err.status === 404) {
			throw error(404, `Feature flag '${params.key}' was not found.`);
		}

		console.error('Failed to load feature flag:', err);
		throw error(500, 'Failed to load the feature flag.');
	}
};
