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
		return json(
			{ error: err instanceof Error ? err.message : 'Failed to create recipe.' },
			{ status: 500 }
		);
	}
};
