import { error } from '@sveltejs/kit';
import { fetchRecipes } from '$lib/api/recipeApi';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent }) => {
	const { session } = await parent();

	try {
		const recipes = await fetchRecipes(session.accessToken);

		// Get only the 5 most recent recipes for the dashboard
		const recentRecipes = recipes.slice(0, 5);

		return {
			recipes: recentRecipes,
			totalRecipes: recipes.length
		};
	} catch (err) {
		console.error('Failed to load dashboard data:', err);
		error(500, 'Failed to load dashboard data. Please try again.');
	}
};
