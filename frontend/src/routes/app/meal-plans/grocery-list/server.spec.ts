import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { RequestEvent } from '@sveltejs/kit';
import { ApiError } from '$lib/api/recipeApi';
import { POST } from './+server';
import { generateGroceryList } from '$lib/api/groceryListApi';

vi.mock('$lib/api/groceryListApi', () => ({
	generateGroceryList: vi.fn()
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

describe('POST /app/meal-plans/grocery-list', () => {
	beforeEach(() => {
		vi.clearAllMocks();
	});

	it('returns 401 when session has no access token', async () => {
		const event = createEvent(
			'http://localhost/app/meal-plans/grocery-list?weekStart=2026-03-09',
			null
		);

		await expect(POST(event)).rejects.toMatchObject({ status: 401 });
	});

	it('generates grocery list and returns payload', async () => {
		const groceryList = {
			id: 'list-1',
			weekStart: '2026-03-09',
			items: [],
			pantryStapleItems: [],
			createdAt: '2026-03-09T00:00:00Z',
			updatedAt: '2026-03-09T00:00:00Z'
		};
		vi.mocked(generateGroceryList).mockResolvedValue(groceryList);

		const event = createEvent(
			'http://localhost/app/meal-plans/grocery-list?weekStart=2026-03-09',
			'test-token'
		);

		const response = await POST(event);
		expect(response.status).toBe(200);
		expect(await response.json()).toEqual({ groceryList });
		expect(generateGroceryList).toHaveBeenCalledWith('test-token', '2026-03-09', event.fetch);
	});

	it('returns 400 for invalid weekStart', async () => {
		const event = createEvent('http://localhost/app/meal-plans/grocery-list?weekStart=bad', 'test-token');
		const response = await POST(event);
		expect(response.status).toBe(400);
		expect(await response.json()).toEqual({
			error: 'weekStart is required and must be in yyyy-MM-dd format.'
		});
	});

	it('passes through ApiError status and message', async () => {
		vi.mocked(generateGroceryList).mockRejectedValue(
			new ApiError(409, 'A grocery list already exists for this week.', { code: 'already_exists' })
		);

		const event = createEvent(
			'http://localhost/app/meal-plans/grocery-list?weekStart=2026-03-09',
			'test-token'
		);

		const response = await POST(event);
		expect(response.status).toBe(409);
		expect(await response.json()).toEqual({
			error: 'A grocery list already exists for this week.',
			body: { code: 'already_exists' }
		});
	});
});
