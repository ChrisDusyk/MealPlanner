import { json, error } from '@sveltejs/kit';
import { generateGroceryList } from '$lib/api/groceryListApi';
import { ApiError } from '$lib/api/recipeApi';
import type { RequestHandler } from './$types';

export const POST: RequestHandler = async ({ locals, fetch, url }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	const weekStart = url.searchParams.get('weekStart')?.trim() ?? '';
	if (!/^\d{4}-\d{2}-\d{2}$/.test(weekStart)) {
		return json({ error: 'weekStart is required and must be in yyyy-MM-dd format.' }, { status: 400 });
	}

	try {
		const groceryList = await generateGroceryList(session.accessToken, weekStart, fetch);
		return json({ groceryList });
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message, body: err.body }, { status: err.status });
		}

		console.error('Meal plan grocery list generation failed:', err);
		return json({ error: 'Failed to generate grocery list' }, { status: 500 });
	}
};
