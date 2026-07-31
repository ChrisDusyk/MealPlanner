import { error, fail } from '@sveltejs/kit';
import { getFeatureFlags, setFeatureFlagEnabled } from '$lib/api/featureFlagsApi';
import { ApiError } from '$lib/api/apiHelpers';
import { requireRole } from '$lib/auth/guards';
import { APP_ROLES } from '$lib/auth/roles';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, locals, fetch }) => {
	const { session } = await parent();
	requireRole(await locals.auth(), APP_ROLES.admin);

	try {
		const flags = await getFeatureFlags(session.accessToken, fetch);
		return { flags };
	} catch (err) {
		console.error('Failed to load feature flags:', err);
		throw error(500, 'Failed to load feature flags.');
	}
};

export const actions: Actions = {
	toggle: async ({ request, locals, fetch }) => {
		const session = requireRole(await locals.auth(), APP_ROLES.admin);

		const form = await request.formData();
		const key = form.get('key');
		const enabled = form.get('enabled') === 'true';

		if (typeof key !== 'string' || key.length === 0) {
			return fail(400, { message: 'A feature flag key is required.' });
		}

		try {
			const flag = await setFeatureFlagEnabled(session.accessToken, key, enabled, fetch);
			return { flag };
		} catch (err) {
			const message = err instanceof ApiError ? err.message : 'Failed to update the feature flag.';
			const status = err instanceof ApiError ? err.status : 500;
			return fail(status, { message });
		}
	}
};
