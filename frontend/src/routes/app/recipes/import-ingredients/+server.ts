import { json, error } from '@sveltejs/kit';
import { importRecipeIngredients, ApiError } from '$lib/api/recipeApi';
import type { RequestHandler } from './$types';

export const POST: RequestHandler = async ({ request, locals, fetch }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	try {
		const body = await request.json();
		const imported = await importRecipeIngredients(session.accessToken, body, fetch);
		return json(imported, { status: 200 });
	} catch (err) {
		console.error('Failed to import recipe ingredients:', err);
		if (err instanceof ApiError) {
			return json({ error: err.message }, { status: err.status });
		}
		const message = err instanceof Error ? err.message : 'Failed to import ingredients.';
		return json({ error: message }, { status: 500 });
	}
};
