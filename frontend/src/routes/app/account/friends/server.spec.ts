import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '$lib/api/recipeApi';
import { DELETE, GET, PATCH, POST } from './+server';
import {
	acceptFriendRequest,
	getFriends,
	getIncomingFriendRequests,
	getOutgoingFriendRequests,
	rejectFriendRequest,
	removeFriend,
	sendFriendRequestByEmail,
	updateFriendAutoSharePreferences
} from '$lib/api/friendsApi';

vi.mock('$lib/api/friendsApi', () => ({
	getFriends: vi.fn(),
	getIncomingFriendRequests: vi.fn(),
	getOutgoingFriendRequests: vi.fn(),
	sendFriendRequestByEmail: vi.fn(),
	acceptFriendRequest: vi.fn(),
	rejectFriendRequest: vi.fn(),
	removeFriend: vi.fn(),
	updateFriendAutoSharePreferences: vi.fn()
}));

function createEvent({
	url,
	accessToken,
	method,
	body,
	rawBody,
	contentType
}: {
	url: string;
	accessToken: string | null;
	method?: string;
	body?: unknown;
	rawBody?: string;
	contentType?: string;
}): Parameters<typeof GET>[0] {
	const resolvedBody = rawBody ?? (body ? JSON.stringify(body) : undefined);
	const resolvedContentType =
		contentType ?? (body || rawBody !== undefined ? 'application/json' : undefined);

	return {
		locals: {
			auth: vi.fn().mockResolvedValue(accessToken ? { accessToken } : null)
		},
		fetch: vi.fn(),
		url: new URL(url),
		request: new Request(url, {
			method: method ?? 'GET',
			body: resolvedBody,
			headers: resolvedContentType ? { 'content-type': resolvedContentType } : undefined
		})
	} as unknown as Parameters<typeof GET>[0];
}

describe('GET /app/account/friends', () => {
	beforeEach(() => {
		vi.clearAllMocks();
	});

	it('returns 401 when session has no access token', async () => {
		const event = createEvent({ url: 'http://localhost/app/account/friends', accessToken: null });
		await expect(GET(event)).rejects.toMatchObject({ status: 401 });
	});

	it('returns bundled friends payload', async () => {
		vi.mocked(getFriends).mockResolvedValue([
			{
				userId: 'u1',
				name: 'Friend',
				email: 'f@example.com',
				autoShareMealPlans: false,
				autoShareGroceryLists: false
			}
		]);
		vi.mocked(getIncomingFriendRequests).mockResolvedValue([]);
		vi.mocked(getOutgoingFriendRequests).mockResolvedValue([]);

		const event = createEvent({
			url: 'http://localhost/app/account/friends',
			accessToken: 'token'
		});
		const response = await GET(event);

		expect(response.status).toBe(200);
		expect(await response.json()).toEqual({
			friends: [
				{
					userId: 'u1',
					name: 'Friend',
					email: 'f@example.com',
					autoShareMealPlans: false,
					autoShareGroceryLists: false
				}
			],
			incoming: [],
			outgoing: []
		});
	});
});

describe('POST /app/account/friends', () => {
	beforeEach(() => {
		vi.clearAllMocks();
	});

	it('sends friend request by email', async () => {
		vi.mocked(sendFriendRequestByEmail).mockResolvedValue({ status: 'Pending' });
		const event = createEvent({
			url: 'http://localhost/app/account/friends',
			accessToken: 'token',
			method: 'POST',
			body: { action: 'send', email: 'pal@example.com' }
		});

		const response = await POST(event);
		expect(response.status).toBe(200);
		expect(await response.json()).toEqual({ status: 'Pending' });
		expect(sendFriendRequestByEmail).toHaveBeenCalledWith('token', 'pal@example.com', event.fetch);
	});

	it('passes through API error status', async () => {
		vi.mocked(sendFriendRequestByEmail).mockRejectedValue(
			new ApiError(400, 'No user exists with that email address.')
		);
		const event = createEvent({
			url: 'http://localhost/app/account/friends',
			accessToken: 'token',
			method: 'POST',
			body: { action: 'send', email: 'missing@example.com' }
		});

		const response = await POST(event);
		expect(response.status).toBe(400);
		expect(await response.json()).toEqual({ error: 'No user exists with that email address.' });
	});

	it('returns 400 for invalid JSON payload', async () => {
		const event = createEvent({
			url: 'http://localhost/app/account/friends',
			accessToken: 'token',
			method: 'POST',
			rawBody: '{"action":"send",',
			contentType: 'application/json'
		});

		const response = await POST(event);
		expect(response.status).toBe(400);
		expect(await response.json()).toEqual({ error: 'Invalid JSON payload.' });
	});

	it('accepts incoming request', async () => {
		vi.mocked(acceptFriendRequest).mockResolvedValue({ success: true });
		const event = createEvent({
			url: 'http://localhost/app/account/friends',
			accessToken: 'token',
			method: 'POST',
			body: { action: 'accept', requestId: 'r1' }
		});

		const response = await POST(event);
		expect(response.status).toBe(200);
		expect(await response.json()).toEqual({ success: true });
		expect(acceptFriendRequest).toHaveBeenCalledWith('token', 'r1', event.fetch);
	});

	it('rejects incoming request', async () => {
		vi.mocked(rejectFriendRequest).mockResolvedValue({ success: true });
		const event = createEvent({
			url: 'http://localhost/app/account/friends',
			accessToken: 'token',
			method: 'POST',
			body: { action: 'reject', requestId: 'r1' }
		});

		const response = await POST(event);
		expect(response.status).toBe(200);
		expect(await response.json()).toEqual({ success: true });
		expect(rejectFriendRequest).toHaveBeenCalledWith('token', 'r1', event.fetch);
	});
});

describe('PATCH /app/account/friends', () => {
	beforeEach(() => {
		vi.clearAllMocks();
	});

	it('updates friend auto-share preferences', async () => {
		vi.mocked(updateFriendAutoSharePreferences).mockResolvedValue({
			autoShareMealPlans: true,
			autoShareGroceryLists: false
		});

		const event = createEvent({
			url: 'http://localhost/app/account/friends',
			accessToken: 'token',
			method: 'PATCH',
			body: {
				friendUserId: 'auth0|friend',
				autoShareMealPlans: true,
				autoShareGroceryLists: false
			}
		});

		const response = await PATCH(event);
		expect(response.status).toBe(200);
		expect(await response.json()).toEqual({
			autoShareMealPlans: true,
			autoShareGroceryLists: false
		});
		expect(updateFriendAutoSharePreferences).toHaveBeenCalledWith(
			'token',
			'auth0|friend',
			{ autoShareMealPlans: true, autoShareGroceryLists: false },
			event.fetch
		);
	});

	it('returns 400 when friendUserId is missing', async () => {
		const event = createEvent({
			url: 'http://localhost/app/account/friends',
			accessToken: 'token',
			method: 'PATCH',
			body: {
				autoShareMealPlans: true,
				autoShareGroceryLists: false
			}
		});

		const response = await PATCH(event);
		expect(response.status).toBe(400);
		expect(updateFriendAutoSharePreferences).not.toHaveBeenCalled();
	});

	it('returns 400 when auto-share values are not boolean', async () => {
		const event = createEvent({
			url: 'http://localhost/app/account/friends',
			accessToken: 'token',
			method: 'PATCH',
			body: {
				friendUserId: 'auth0|friend',
				// invalid types
				autoShareMealPlans: 'yes',
				autoShareGroceryLists: null
			}
		});

		const response = await PATCH(event);
		expect(response.status).toBe(400);
		expect(updateFriendAutoSharePreferences).not.toHaveBeenCalled();
	});

	it('returns 400 when request body is invalid JSON', async () => {
		const event = createEvent({
			url: 'http://localhost/app/account/friends',
			accessToken: 'token',
			method: 'PATCH',
			// simulate invalid JSON body
			body: 'this is not valid JSON'
		});

		const response = await PATCH(event);
		expect(response.status).toBe(400);
		expect(updateFriendAutoSharePreferences).not.toHaveBeenCalled();
	});

	it('propagates ApiError status from updateFriendAutoSharePreferences', async () => {
		vi.mocked(updateFriendAutoSharePreferences).mockRejectedValue(
			new ApiError(409, 'Conflict updating auto-share preferences')
		);

		const event = createEvent({
			url: 'http://localhost/app/account/friends',
			accessToken: 'token',
			method: 'PATCH',
			body: {
				friendUserId: 'auth0|friend',
				autoShareMealPlans: true,
				autoShareGroceryLists: false
			}
		});

		const response = await PATCH(event);
		expect(response.status).toBe(409);
	});
});

describe('DELETE /app/account/friends', () => {
	beforeEach(() => {
		vi.clearAllMocks();
	});

	it('removes friend', async () => {
		vi.mocked(removeFriend).mockResolvedValue({ success: true });
		const event = createEvent({
			url: 'http://localhost/app/account/friends?friendUserId=auth0%7Cfriend',
			accessToken: 'token',
			method: 'DELETE'
		});

		const response = await DELETE(event);
		expect(response.status).toBe(200);
		expect(await response.json()).toEqual({ success: true });
		expect(removeFriend).toHaveBeenCalledWith('token', 'auth0|friend', event.fetch);
	});
});
