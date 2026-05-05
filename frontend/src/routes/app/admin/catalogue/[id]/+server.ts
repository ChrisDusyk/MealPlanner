import { json, error } from '@sveltejs/kit';
import {
	ApiError,
	adminDeleteCatalogueRecipe,
	adminUpdateCatalogueRecipe,
	type UpdateCatalogueRecipeRequest
} from '$lib/api/adminCatalogueApi';
import { requireRole } from '$lib/auth/guards';
import { APP_ROLES } from '$lib/auth/roles';
import type { RequestHandler } from './$types';

export const PUT: RequestHandler = async ({ request, params, locals, fetch }) => {
	const session = requireRole(await locals.auth(), APP_ROLES.admin);
	if (!session.accessToken) {
		error(401, 'Unauthorized');
	}

	let body: UpdateCatalogueRecipeRequest;
	try {
		body = (await request.json()) as UpdateCatalogueRecipeRequest;
	} catch {
		return json({ error: 'Invalid JSON payload.' }, { status: 400 });
	}

	try {
		const updated = await adminUpdateCatalogueRecipe(session.accessToken, params.id, body, fetch);
		return json(updated, { status: 200 });
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message, body: err.body }, { status: err.status });
		}

		const message = err instanceof Error ? err.message : 'Failed to update catalogue recipe.';
		return json({ error: message }, { status: 500 });
	}
};

export const DELETE: RequestHandler = async ({ params, locals, fetch }) => {
	const session = requireRole(await locals.auth(), APP_ROLES.admin);
	if (!session.accessToken) {
		error(401, 'Unauthorized');
	}

	try {
		await adminDeleteCatalogueRecipe(session.accessToken, params.id, fetch);
		return new Response(null, { status: 204 });
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message, body: err.body }, { status: err.status });
		}

		const message = err instanceof Error ? err.message : 'Failed to delete catalogue recipe.';
		return json({ error: message }, { status: 500 });
	}
};
