import { json, error, type RequestEvent } from '@sveltejs/kit';
import { ApiError } from '$lib/api/recipeApi';
import { addCustomItem } from '$lib/api/groceryListApi';

/**
 * POST — Add a custom item to a grocery list shared with the current user.
 */
export async function POST({ locals, fetch, request, url }: RequestEvent): Promise<Response> {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	const weekStart = url.searchParams.get('weekStart')?.trim() ?? '';
	const ownerUserId = url.searchParams.get('ownerUserId')?.trim() ?? '';

	if (!/^\d{4}-\d{2}-\d{2}$/.test(weekStart)) {
		return json(
			{ error: 'weekStart is required and must be in yyyy-MM-dd format.' },
			{ status: 400 }
		);
	}

	if (!ownerUserId) {
		return json({ error: 'ownerUserId is required' }, { status: 400 });
	}

	const body = await request.json().catch(() => ({}));
	const name = typeof body?.name === 'string' ? body.name.trim() : '';
	if (!name) {
		return json({ error: 'name is required and cannot be empty.' }, { status: 400 });
	}

	try {
		const updated = await addCustomItem(session.accessToken, weekStart, name, fetch, ownerUserId);
		return json(updated);
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message, body: err.body }, { status: err.status });
		}
		console.error('Add shared grocery item failed:', err);
		return json({ error: 'Failed to add item to shared grocery list' }, { status: 500 });
	}
}
