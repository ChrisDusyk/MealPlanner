import { error } from '@sveltejs/kit';
import { fetchRecipeById, ApiError } from '$lib/api/recipeApi';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, params, fetch }) => {
	const { session } = await parent();

	try {
		const recipe = await fetchRecipeById(session.accessToken, params.id, fetch);
		return { recipe };
	} catch (err) {
		if (err instanceof ApiError && err.status === 404) {
			error(404, 'Recipe not found.');
		}
		console.error('Failed to load recipe:', err);
		error(500, 'Failed to load recipe. Please try again.');
	}
};
