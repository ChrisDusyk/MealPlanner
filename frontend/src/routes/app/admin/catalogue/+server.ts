import { json, error } from '@sveltejs/kit';
import {
	ApiError,
	adminCreateCatalogueRecipe,
	type CreateCatalogueRecipeRequest
} from '$lib/api/adminCatalogueApi';
import { requireRole } from '$lib/auth/guards';
import { APP_ROLES } from '$lib/auth/roles';
import type { RequestHandler } from './$types';

export const POST: RequestHandler = async ({ request, locals, fetch }) => {
	const session = requireRole(await locals.auth(), APP_ROLES.admin);
	if (!session.accessToken) {
		error(401, 'Unauthorized');
	}

	let body: CreateCatalogueRecipeRequest;
	try {
		body = (await request.json()) as CreateCatalogueRecipeRequest;
	} catch {
		return json({ error: 'Invalid JSON payload.' }, { status: 400 });
	}

	try {
		const created = await adminCreateCatalogueRecipe(session.accessToken, body, fetch);
		return json(created, { status: 201 });
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message, body: err.body }, { status: err.status });
		}

		const message = err instanceof Error ? err.message : 'Failed to create catalogue recipe.';
		return json({ error: message }, { status: 500 });
	}
};
