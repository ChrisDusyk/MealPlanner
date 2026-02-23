import { error } from '@sveltejs/kit';
import { fetchMealPlan } from '$lib/api/mealPlanApi';
import { fetchRecipes } from '$lib/api/recipeApi';
import { getSharedWithMe, getMyShares } from '$lib/api/sharingApi';
import type { SharedMealPlanResponse, MealPlanShareResponse } from '$lib/api/sharingApi';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, fetch, url }) => {
	const { session } = await parent();

	const weekStart = url.searchParams.get('weekStart') ?? undefined;

	try {
		const [mealPlan, recipes, sharedWithMe, myShares] = await Promise.all([
			fetchMealPlan(session.accessToken, weekStart, fetch),
			fetchRecipes(session.accessToken, fetch),
			getSharedWithMe(session.accessToken, weekStart ?? '', fetch).catch(
				(): SharedMealPlanResponse[] => []
			),
			getMyShares(session.accessToken, weekStart ?? '', fetch).catch(
				(): MealPlanShareResponse[] => []
			)
		]);

		return {
			mealPlan,
			recipes,
			sharedWithMe,
			myShares
		};
	} catch (err) {
		console.error('Failed to load meal plan data:', err);
		error(500, 'Failed to load meal plan data. Please try again.');
	}
};
