import { json, error } from '@sveltejs/kit';
import { createRecipe } from '$lib/api/recipeApi';
import type { RequestHandler } from './$types';

export const POST: RequestHandler = async ({ request, locals, fetch }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	try {
		const body = await request.json();
		const recipe = await createRecipe(session.accessToken, body, fetch);
		return json(recipe, { status: 201 });
	} catch (err) {
		console.error('Failed to create recipe:', err);
		// Preserve the original status code from the API when possible
		const message = err instanceof Error ? err.message : 'Failed to create recipe.';
		const isBadRequest = message.includes('400');
		return json({ error: message }, { status: isBadRequest ? 400 : 500 });
	}
};
