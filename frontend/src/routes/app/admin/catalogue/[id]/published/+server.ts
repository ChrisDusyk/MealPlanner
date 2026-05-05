import { json, error } from '@sveltejs/kit';
import {
	ApiError,
	adminSetCatalogueRecipePublished,
	type SetCatalogueRecipePublishedRequest
} from '$lib/api/adminCatalogueApi';
import { requireRole } from '$lib/auth/guards';
import { APP_ROLES } from '$lib/auth/roles';
import type { RequestHandler } from './$types';

export const PUT: RequestHandler = async ({ request, params, locals, fetch }) => {
	const session = requireRole(await locals.auth(), APP_ROLES.admin);
	if (!session.accessToken) {
		error(401, 'Unauthorized');
	}

	let body: SetCatalogueRecipePublishedRequest;
	try {
		body = (await request.json()) as SetCatalogueRecipePublishedRequest;
	} catch {
		return json({ error: 'Invalid JSON payload.' }, { status: 400 });
	}

	try {
		const updated = await adminSetCatalogueRecipePublished(
			session.accessToken,
			params.id,
			body.isPublished,
			fetch
		);
		return json(updated, { status: 200 });
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message, body: err.body }, { status: err.status });
		}

		const message =
			err instanceof Error ? err.message : 'Failed to update catalogue publish state.';
		return json({ error: message }, { status: 500 });
	}
};
