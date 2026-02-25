import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { RequestEvent } from '@sveltejs/kit';
import { ApiError } from '$lib/api/recipeApi';
import { PUT } from './+server';
import { toggleGroceryListItem } from '$lib/api/groceryListApi';

vi.mock('$lib/api/groceryListApi', () => ({
	toggleGroceryListItem: vi.fn()
}));

function createEvent(url: string, accessToken: string | null): RequestEvent {
	return {
		locals: {
			auth: vi.fn().mockResolvedValue(accessToken ? { accessToken } : null)
		},
		fetch: vi.fn(),
		url: new URL(url)
	} as unknown as RequestEvent;
}

describe('PUT /app/grocery-lists/toggle-shared', () => {
	beforeEach(() => {
		vi.clearAllMocks();
	});

	it('returns 401 when session has no access token', async () => {
		const event = createEvent(
			'http://localhost/app/grocery-lists/toggle-shared?weekStart=2026-02-23&ownerUserId=owner-1&itemIndex=2',
			null
		);

		await expect(PUT(event)).rejects.toMatchObject({ status: 401 });
	});

	it('proxies shared toggle using session access token and ownerUserId', async () => {
		const updated = {
			id: 'list-1',
			weekStart: '2026-02-23',
			items: [],
			createdAt: '2026-02-23T00:00:00Z',
			updatedAt: '2026-02-23T00:00:00Z'
		};
		vi.mocked(toggleGroceryListItem).mockResolvedValue(updated);

		const event = createEvent(
			'http://localhost/app/grocery-lists/toggle-shared?weekStart=2026-02-23&ownerUserId=owner-1&itemIndex=2',
			'test-token'
		);

		const response = await PUT(event);
		expect(response.status).toBe(200);
		expect(await response.json()).toEqual(updated);
		expect(toggleGroceryListItem).toHaveBeenCalledWith(
			'test-token',
			'2026-02-23',
			2,
			event.fetch,
			'owner-1'
		);
	});

	it('passes through API error status and message', async () => {
		vi.mocked(toggleGroceryListItem).mockRejectedValue(
			new ApiError(403, 'You only have read-only access to this grocery list.', { code: 'forbidden' })
		);

		const event = createEvent(
			'http://localhost/app/grocery-lists/toggle-shared?weekStart=2026-02-23&ownerUserId=owner-1&itemIndex=2',
			'test-token'
		);

		const response = await PUT(event);
		expect(response.status).toBe(403);
		expect(await response.json()).toEqual({
			error: 'You only have read-only access to this grocery list.',
			body: { code: 'forbidden' }
		});
	});
});
