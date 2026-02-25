import { json, error, type RequestEvent } from '@sveltejs/kit';
import {
	shareGroceryList,
	getMyGroceryListShares,
	revokeGroceryListShare,
	getGroceryListsSharedWithMe,
	dismissGroceryListShare
} from '$lib/api/sharingApi';
import { ApiError } from '$lib/api/recipeApi';

/**
 * POST — Share a grocery list OR dismiss a shared one.
 * Distinguish by the `action` query param.
 */
export async function POST({ request, locals, fetch, url }: RequestEvent): Promise<Response> {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	const action = url.searchParams.get('action');

	try {
		if (action === 'dismiss') {
			const shareId = url.searchParams.get('shareId') ?? '';
			if (!shareId) {
				return json({ error: 'shareId is required' }, { status: 400 });
			}
			await dismissGroceryListShare(session.accessToken, shareId, fetch);
			return json({ ok: true });
		}

		// Default: create share
		const body = await request.json();
		const result = await shareGroceryList(session.accessToken, body, fetch);
		return json(result, { status: 201 });
	} catch (err) {
		console.error('Grocery list sharing POST failed:', err);
		if (err instanceof ApiError) {
			return json({ error: err.message }, { status: err.status });
		}
		const message = err instanceof Error ? err.message : 'Sharing operation failed.';
		return json({ error: message }, { status: 500 });
	}
}

/**
 * GET — Fetch grocery list shares.
 * ?type=my-shares&weekStart=…  → shares the current user owns
 * ?type=shared-with-me&weekStart=…  → grocery lists shared WITH the current user
 */
export async function GET({ locals, fetch, url }: RequestEvent): Promise<Response> {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	const type = url.searchParams.get('type') ?? 'shared-with-me';
	const weekStart = url.searchParams.get('weekStart') ?? '';

	try {
		if (type === 'my-shares') {
			const shares = await getMyGroceryListShares(session.accessToken, weekStart, fetch);
			return json(shares);
		}

		const shared = await getGroceryListsSharedWithMe(session.accessToken, weekStart, fetch);
		return json(shared);
	} catch (err) {
		console.error('Grocery list sharing GET failed:', err);
		if (err instanceof ApiError) {
			return json({ error: err.message }, { status: err.status });
		}
		const message = err instanceof Error ? err.message : 'Failed to load shares.';
		return json({ error: message }, { status: 500 });
	}
}

/**
 * DELETE — Revoke a grocery list share by shareId.
 */
export async function DELETE({ locals, fetch, url }: RequestEvent): Promise<Response> {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	const shareId = url.searchParams.get('shareId') ?? '';
	if (!shareId) {
		return json({ error: 'shareId is required' }, { status: 400 });
	}

	try {
		await revokeGroceryListShare(session.accessToken, shareId, fetch);
		return json({ ok: true });
	} catch (err) {
		console.error('Grocery list sharing DELETE failed:', err);
		if (err instanceof ApiError) {
			return json({ error: err.message }, { status: err.status });
		}
		const message = err instanceof Error ? err.message : 'Failed to revoke share.';
		return json({ error: message }, { status: 500 });
	}
}
