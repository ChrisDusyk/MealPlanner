import { json, error } from '@sveltejs/kit';
import {
	shareMealPlan,
	getMyShares,
	revokeShare,
	getSharedWithMe,
	dismissShare
} from '$lib/api/sharingApi';
import type { RequestHandler } from './$types';

/**
 * POST — Share a meal plan OR dismiss a shared plan.
 * Distinguish by the `action` query param.
 */
export const POST: RequestHandler = async ({ request, locals, fetch, url }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	const action = url.searchParams.get('action');

	try {
		if (action === 'dismiss') {
			const shareId = url.searchParams.get('shareId') ?? '';
			await dismissShare(session.accessToken, shareId, fetch);
			return json({ ok: true });
		}

		// Default: create share
		const body = await request.json();
		const result = await shareMealPlan(session.accessToken, body, fetch);
		return json(result, { status: 201 });
	} catch (err) {
		console.error('Sharing POST failed:', err);
		const message = err instanceof Error ? err.message : 'Sharing operation failed.';
		return json({ error: message }, { status: 500 });
	}
};

/**
 * GET — Fetch shares.
 * ?type=my-shares&weekStart=…  → shares the current user owns
 * ?type=shared-with-me&weekStart=…  → plans shared WITH the current user
 */
export const GET: RequestHandler = async ({ locals, fetch, url }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	const type = url.searchParams.get('type') ?? 'shared-with-me';
	const weekStart = url.searchParams.get('weekStart') ?? '';

	try {
		if (type === 'my-shares') {
			const shares = await getMyShares(session.accessToken, weekStart, fetch);
			return json(shares);
		}

		const shared = await getSharedWithMe(session.accessToken, weekStart, fetch);
		return json(shared);
	} catch (err) {
		console.error('Sharing GET failed:', err);
		const message = err instanceof Error ? err.message : 'Failed to load shares.';
		return json({ error: message }, { status: 500 });
	}
};

/**
 * DELETE — Revoke a share by shareId.
 */
export const DELETE: RequestHandler = async ({ locals, fetch, url }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	const shareId = url.searchParams.get('shareId') ?? '';

	try {
		await revokeShare(session.accessToken, shareId, fetch);
		return json({ ok: true });
	} catch (err) {
		console.error('Sharing DELETE failed:', err);
		const message = err instanceof Error ? err.message : 'Failed to revoke share.';
		return json({ error: message }, { status: 500 });
	}
};
