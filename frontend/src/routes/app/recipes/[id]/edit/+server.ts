import { json, error } from '@sveltejs/kit';
import { updateRecipe, ApiError } from '$lib/api/recipeApi';
import type { RequestHandler } from './$types';

export const PUT: RequestHandler = async ({ request, locals, params, fetch }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	try {
		const body = await request.json();
		const recipe = await updateRecipe(session.accessToken, params.id, body, fetch);
		return json(recipe, { status: 200 });
	} catch (err) {
		console.error('Failed to update recipe:', err);
		if (err instanceof ApiError) {
			return json({ error: err.message }, { status: err.status });
		}
		const message = err instanceof Error ? err.message : 'Failed to update recipe.';
		return json({ error: message }, { status: 500 });
	}
};
