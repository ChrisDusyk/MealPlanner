import { json, error } from '@sveltejs/kit';
import {
	ApiError,
	addCatalogueRecipeToMyRecipes,
	fetchCatalogueRecipe
} from '$lib/api/catalogueApi';
import { requireAuthenticatedSession } from '$lib/auth/guards';
import type { RequestHandler } from './$types';

export const GET: RequestHandler = async ({ params, locals, fetch }) => {
	const session = requireAuthenticatedSession(await locals.auth());
	if (!session.accessToken) {
		error(401, 'Unauthorized');
	}

	try {
		const item = await fetchCatalogueRecipe(session.accessToken, params.id, fetch);
		return json(item);
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message, body: err.body }, { status: err.status });
		}

		const message = err instanceof Error ? err.message : 'Failed to load catalogue recipe.';
		return json({ error: message }, { status: 500 });
	}
};

export const POST: RequestHandler = async ({ params, locals, fetch }) => {
	const session = requireAuthenticatedSession(await locals.auth());
	if (!session.accessToken) {
		error(401, 'Unauthorized');
	}

	try {
		const response = await addCatalogueRecipeToMyRecipes(session.accessToken, params.id, fetch);
		return json(response, { status: 200 });
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message, body: err.body }, { status: err.status });
		}

		const message = err instanceof Error ? err.message : 'Failed to add catalogue recipe.';
		return json({ error: message }, { status: 500 });
	}
};
