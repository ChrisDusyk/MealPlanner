import { json, error } from '@sveltejs/kit';
import {
	acceptFriendRequest,
	getFriends,
	getIncomingFriendRequests,
	getOutgoingFriendRequests,
	rejectFriendRequest,
	removeFriend,
	sendFriendRequestByEmail
} from '$lib/api/friendsApi';
import { ApiError } from '$lib/api/recipeApi';
import type { RequestHandler } from './$types';

export const GET: RequestHandler = async ({ locals, fetch }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	try {
		const [friends, incoming, outgoing] = await Promise.all([
			getFriends(session.accessToken, fetch),
			getIncomingFriendRequests(session.accessToken, fetch),
			getOutgoingFriendRequests(session.accessToken, fetch)
		]);

		return json({ friends, incoming, outgoing });
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message }, { status: err.status });
		}
		const message = err instanceof Error ? err.message : 'Failed to load friends.';
		return json({ error: message }, { status: 500 });
	}
};

export const POST: RequestHandler = async ({ request, locals, fetch }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	const body = (await request.json()) as {
		action?: 'send' | 'accept' | 'reject';
		email?: string;
		requestId?: string;
	};

	if (!body.action) {
		return json({ error: 'action is required' }, { status: 400 });
	}

	try {
		switch (body.action) {
			case 'send': {
				const email = body.email?.trim() ?? '';
				if (!email) {
					return json({ error: 'email is required' }, { status: 400 });
				}
				const result = await sendFriendRequestByEmail(session.accessToken, email, fetch);
				return json(result);
			}
			case 'accept': {
				const requestId = body.requestId?.trim() ?? '';
				if (!requestId) {
					return json({ error: 'requestId is required' }, { status: 400 });
				}
				const result = await acceptFriendRequest(session.accessToken, requestId, fetch);
				return json(result);
			}
			case 'reject': {
				const requestId = body.requestId?.trim() ?? '';
				if (!requestId) {
					return json({ error: 'requestId is required' }, { status: 400 });
				}
				const result = await rejectFriendRequest(session.accessToken, requestId, fetch);
				return json(result);
			}
			default:
				return json({ error: 'Unsupported action.' }, { status: 400 });
		}
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message }, { status: err.status });
		}
		const message = err instanceof Error ? err.message : 'Failed to process friend action.';
		return json({ error: message }, { status: 500 });
	}
};

export const DELETE: RequestHandler = async ({ locals, fetch, url }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	const friendUserId = url.searchParams.get('friendUserId')?.trim() ?? '';
	if (!friendUserId) {
		return json({ error: 'friendUserId is required' }, { status: 400 });
	}

	try {
		const result = await removeFriend(session.accessToken, friendUserId, fetch);
		return json(result);
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message }, { status: err.status });
		}
		const message = err instanceof Error ? err.message : 'Failed to remove friend.';
		return json({ error: message }, { status: 500 });
	}
};
