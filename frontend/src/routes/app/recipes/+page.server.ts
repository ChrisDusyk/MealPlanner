import { error } from '@sveltejs/kit';
import { fetchRecipes } from '$lib/api/recipeApi';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent }) => {
	const { session } = await parent();

	try {
		const recipes = await fetchRecipes(session.accessToken);
		return { recipes };
	} catch (err) {
		console.error('Failed to load recipes:', err);
		error(500, 'Failed to load recipes. Please try again.');
	}
};
