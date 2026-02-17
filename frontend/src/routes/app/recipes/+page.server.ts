import { fetchRecipes } from '$lib/api/recipeApi';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent }) => {
	const { session } = await parent();

	try {
		const recipes = await fetchRecipes(session.accessToken);
		return { recipes };
	} catch {
		return { recipes: [] };
	}
};
