import { ApiError } from '$lib/api/recipeApi';
import {
	fetchGroceryList,
	generateGroceryList,
	toggleGroceryListItem,
	addCustomItem,
	deleteGroceryList
} from '$lib/api/groceryListApi';
import type { GroceryListResponse } from '$lib/api/groceryListApi';
import type { PageServerLoad, Actions } from './$types';
import { fail } from '@sveltejs/kit';

/** Get the Monday of the current week as yyyy-MM-dd */
function getCurrentWeekMonday(): string {
	const now = new Date();
	const day = now.getDay();
	const diff = day === 0 ? -6 : 1 - day; // Monday = 1
	const monday = new Date(now);
	monday.setDate(now.getDate() + diff);
	return monday.toISOString().slice(0, 10);
}

export const load: PageServerLoad = async ({ parent, fetch, url }) => {
	const { session } = await parent();

	const weekStart = url.searchParams.get('weekStart') ?? getCurrentWeekMonday();

	let groceryList: GroceryListResponse | null = null;

	try {
		groceryList = await fetchGroceryList(session.accessToken, weekStart, fetch);
	} catch (err) {
		// 404 means no list generated yet — this is expected
		if (err instanceof ApiError && err.status === 404) {
			groceryList = null;
		} else {
			console.error('Failed to load grocery list:', err);
		}
	}

	return {
		groceryList,
		weekStart
	};
};

export const actions: Actions = {
	generate: async ({ request, locals, fetch }) => {
		const session = await locals.auth();
		if (!session?.accessToken) return fail(401);

		const formData = await request.formData();
		const weekStart = formData.get('weekStart') as string;

		try {
			const list = await generateGroceryList(session.accessToken, weekStart, fetch);
			return { groceryList: list };
		} catch (err) {
			const message = err instanceof Error ? err.message : 'Failed to generate grocery list';
			return fail(500, { error: message });
		}
	},

	toggle: async ({ request, locals, fetch }) => {
		const session = await locals.auth();
		if (!session?.accessToken) return fail(401);

		const formData = await request.formData();
		const weekStart = formData.get('weekStart') as string;
		const itemIndex = parseInt(formData.get('itemIndex') as string, 10);

		try {
			const list = await toggleGroceryListItem(session.accessToken, weekStart, itemIndex, fetch);
			return { groceryList: list };
		} catch (err) {
			const message = err instanceof Error ? err.message : 'Failed to toggle item';
			return fail(500, { error: message });
		}
	},

	addItem: async ({ request, locals, fetch }) => {
		const session = await locals.auth();
		if (!session?.accessToken) return fail(401);

		const formData = await request.formData();
		const weekStart = formData.get('weekStart') as string;
		const name = formData.get('name') as string;

		try {
			const list = await addCustomItem(session.accessToken, weekStart, name, fetch);
			return { groceryList: list };
		} catch (err) {
			const message = err instanceof Error ? err.message : 'Failed to add item';
			return fail(500, { error: message });
		}
	},

	delete: async ({ request, locals, fetch }) => {
		const session = await locals.auth();
		if (!session?.accessToken) return fail(401);

		const formData = await request.formData();
		const weekStart = formData.get('weekStart') as string;

		try {
			await deleteGroceryList(session.accessToken, weekStart, fetch);
			return { groceryList: null };
		} catch (err) {
			const message = err instanceof Error ? err.message : 'Failed to delete grocery list';
			return fail(500, { error: message });
		}
	}
};
