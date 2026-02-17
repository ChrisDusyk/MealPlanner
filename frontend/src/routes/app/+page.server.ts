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
	} catch {
		return {
			recipes: [],
			totalRecipes: 0
		};
	}
};
