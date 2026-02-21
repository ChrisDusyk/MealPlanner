import { error } from '@sveltejs/kit';
import { fetchMealPlan } from '$lib/api/mealPlanApi';
import { fetchRecipes } from '$lib/api/recipeApi';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, fetch, url }) => {
	const { session } = await parent();

	const weekStart = url.searchParams.get('weekStart') ?? undefined;

	try {
		const [mealPlan, recipes] = await Promise.all([
			fetchMealPlan(session.accessToken, weekStart, fetch),
			fetchRecipes(session.accessToken, fetch)
		]);

		return {
			mealPlan,
			recipes
		};
	} catch (err) {
		console.error('Failed to load meal plan data:', err);
		error(500, 'Failed to load meal plan data. Please try again.');
	}
};
