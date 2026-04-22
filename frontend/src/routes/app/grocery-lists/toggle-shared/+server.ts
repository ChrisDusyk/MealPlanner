import { json, error, type RequestEvent } from '@sveltejs/kit';
import { ApiError } from '$lib/api/recipeApi';
import { toggleGroceryListItem } from '$lib/api/groceryListApi';

/**
 * PUT — Toggle an item on a grocery list shared with the current user.
 */
export async function PUT({ locals, fetch, url }: RequestEvent): Promise<Response> {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	const weekStart = url.searchParams.get('weekStart')?.trim() ?? '';
	const ownerUserId = url.searchParams.get('ownerUserId')?.trim() ?? '';
	const itemIndexRaw = url.searchParams.get('itemIndex')?.trim() ?? '';

	if (!/^\d{4}-\d{2}-\d{2}$/.test(weekStart)) {
		return json(
			{ error: 'weekStart is required and must be in yyyy-MM-dd format.' },
			{ status: 400 }
		);
	}

	if (!ownerUserId) {
		return json({ error: 'ownerUserId is required' }, { status: 400 });
	}

	const itemIndex = Number.parseInt(itemIndexRaw, 10);
	if (!Number.isFinite(itemIndex)) {
		return json({ error: 'itemIndex is required and must be a valid integer.' }, { status: 400 });
	}

	try {
		const updated = await toggleGroceryListItem(
			session.accessToken,
			weekStart,
			itemIndex,
			fetch,
			ownerUserId
		);
		return json(updated);
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message, body: err.body }, { status: err.status });
		}
		console.error('Shared grocery list toggle failed:', err);
		return json({ error: 'Failed to toggle shared grocery list item' }, { status: 500 });
	}
}
