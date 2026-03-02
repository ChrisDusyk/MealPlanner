import { ApiError } from '$lib/api/recipeApi';
import {
	fetchGroceryList,
	generateGroceryList,
	toggleGroceryListItem,
	addCustomItem,
	deleteGroceryList,
	promotePantryStapleItem
} from '$lib/api/groceryListApi';
import { getGroceryListsSharedWithMe, type SharedGroceryListResponse } from '$lib/api/sharingApi';
import type { GroceryListResponse } from '$lib/api/groceryListApi';
import type { PageServerLoad, Actions } from './$types';
import { error, fail } from '@sveltejs/kit';

/** Get the Monday of the current week as yyyy-MM-dd */
function getCurrentWeekMonday(): string {
	const now = new Date();
	const day = now.getDay();
	const diff = day === 0 ? -6 : 1 - day; // Monday = 1
	const monday = new Date(now);
	monday.setDate(now.getDate() + diff);
	return monday.toISOString().slice(0, 10);
}

/** Validate that a value is a non-empty string in yyyy-MM-dd format */
function isValidDateString(value: FormDataEntryValue | null): value is string {
	return typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value);
}

export const load: PageServerLoad = async ({ parent, fetch, url }) => {
	const { session } = await parent();

	const weekStart = url.searchParams.get('weekStart') ?? getCurrentWeekMonday();

	let groceryList: GroceryListResponse | null = null;
	let sharedWithMe: SharedGroceryListResponse[] = [];

	try {
		groceryList = await fetchGroceryList(session.accessToken, weekStart, fetch);
	} catch (err) {
		// 404 means no list generated yet — this is expected
		if (err instanceof ApiError && err.status === 404) {
			groceryList = null;
		} else {
			const status = err instanceof ApiError ? err.status : 500;
			const message = err instanceof Error ? err.message : 'Failed to load grocery list';
			throw error(status, message);
		}
	}

	try {
		sharedWithMe = await getGroceryListsSharedWithMe(session.accessToken, weekStart, fetch);
	} catch {
		// Non-fatal: shared lists failing should not break the page
		sharedWithMe = [];
	}

	return {
		groceryList,
		sharedWithMe,
		weekStart
	};
};

export const actions: Actions = {
	generate: async ({ request, locals, fetch }) => {
		const session = await locals.auth();
		if (!session?.accessToken) return fail(401);

		const formData = await request.formData();
		const weekStart = formData.get('weekStart');

		if (!isValidDateString(weekStart)) {
			return fail(400, { error: 'weekStart is required and must be in yyyy-MM-dd format.' });
		}

		try {
			const list = await generateGroceryList(session.accessToken, weekStart, fetch);
			return { groceryList: list };
		} catch (err) {
			const status = err instanceof ApiError ? err.status : 500;
			const message = err instanceof Error ? err.message : 'Failed to generate grocery list';
			return fail(status, { error: message });
		}
	},

	toggle: async ({ request, locals, fetch }) => {
		const session = await locals.auth();
		if (!session?.accessToken) return fail(401);

		const formData = await request.formData();
		const weekStart = formData.get('weekStart');
		const itemIndexRaw = formData.get('itemIndex');

		if (!isValidDateString(weekStart)) {
			return fail(400, { error: 'weekStart is required and must be in yyyy-MM-dd format.' });
		}

		const itemIndex = parseInt(itemIndexRaw as string, 10);
		if (itemIndexRaw === null || !Number.isFinite(itemIndex)) {
			return fail(400, { error: 'itemIndex is required and must be a valid integer.' });
		}

		try {
			const list = await toggleGroceryListItem(session.accessToken, weekStart, itemIndex, fetch);
			return { groceryList: list };
		} catch (err) {
			const status = err instanceof ApiError ? err.status : 500;
			const message = err instanceof Error ? err.message : 'Failed to toggle item';
			return fail(status, { error: message });
		}
	},

	addItem: async ({ request, locals, fetch }) => {
		const session = await locals.auth();
		if (!session?.accessToken) return fail(401);

		const formData = await request.formData();
		const weekStart = formData.get('weekStart');
		const name = formData.get('name');

		if (!isValidDateString(weekStart)) {
			return fail(400, { error: 'weekStart is required and must be in yyyy-MM-dd format.' });
		}

		if (typeof name !== 'string' || name.trim().length === 0) {
			return fail(400, { error: 'name is required and cannot be empty.' });
		}

		try {
			const list = await addCustomItem(session.accessToken, weekStart, name.trim(), fetch);
			return { groceryList: list };
		} catch (err) {
			const status = err instanceof ApiError ? err.status : 500;
			const message = err instanceof Error ? err.message : 'Failed to add item';
			return fail(status, { error: message });
		}
	},

	delete: async ({ request, locals, fetch }) => {
		const session = await locals.auth();
		if (!session?.accessToken) return fail(401);

		const formData = await request.formData();
		const weekStart = formData.get('weekStart');

		if (!isValidDateString(weekStart)) {
			return fail(400, { error: 'weekStart is required and must be in yyyy-MM-dd format.' });
		}

		try {
			await deleteGroceryList(session.accessToken, weekStart, fetch);
			return { groceryList: null };
		} catch (err) {
			const status = err instanceof ApiError ? err.status : 500;
			const message = err instanceof Error ? err.message : 'Failed to delete grocery list';
			return fail(status, { error: message });
		}
	},

	promoteStaple: async ({ request, locals, fetch }) => {
		const session = await locals.auth();
		if (!session?.accessToken) return fail(401);

		const formData = await request.formData();
		const weekStart = formData.get('weekStart');
		const itemIndexRaw = formData.get('itemIndex');

		if (!isValidDateString(weekStart)) {
			return fail(400, { error: 'weekStart is required and must be in yyyy-MM-dd format.' });
		}

		const itemIndex = parseInt(itemIndexRaw as string, 10);
		if (itemIndexRaw === null || !Number.isFinite(itemIndex)) {
			return fail(400, { error: 'itemIndex is required and must be a valid integer.' });
		}

		try {
			const list = await promotePantryStapleItem(session.accessToken, weekStart, itemIndex, fetch);
			return { groceryList: list };
		} catch (err) {
			const status = err instanceof ApiError ? err.status : 500;
			const message = err instanceof Error ? err.message : 'Failed to promote pantry staple item';
			return fail(status, { error: message });
		}
	}
};
