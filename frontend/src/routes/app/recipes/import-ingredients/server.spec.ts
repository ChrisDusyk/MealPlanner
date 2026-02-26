import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, importRecipeIngredients } from '$lib/api/recipeApi';
import { POST } from './+server';

type ImportIngredientsEvent = Parameters<typeof POST>[0];

vi.mock('$lib/api/recipeApi', async () => {
	const actual = await vi.importActual<typeof import('$lib/api/recipeApi')>('$lib/api/recipeApi');
	return {
		...actual,
		importRecipeIngredients: vi.fn()
	};
});

function createEvent({
	accessToken,
	body
}: {
	accessToken: string | null;
	body: unknown;
}): ImportIngredientsEvent {
	return {
		locals: {
			auth: vi.fn().mockResolvedValue(accessToken ? { accessToken } : null)
		},
		fetch: vi.fn(),
		request: new Request('http://localhost/app/recipes/import-ingredients', {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify(body)
		})
	} as unknown as ImportIngredientsEvent;
}

describe('POST /app/recipes/import-ingredients', () => {
	beforeEach(() => {
		vi.clearAllMocks();
	});

	it('returns 401 when session has no access token', async () => {
		const event = createEvent({
			accessToken: null,
			body: { sourceUrl: 'https://example.com/recipe' }
		});

		await expect(POST(event)).rejects.toMatchObject({ status: 401 });
	});

	it('proxies import call and returns payload when successful', async () => {
		const imported = {
			ingredients: [{ name: 'Flour', quantity: 2, unit: 'cups' }],
			warnings: ['Quantity inferred']
		};
		vi.mocked(importRecipeIngredients).mockResolvedValue(imported);

		const event = createEvent({
			accessToken: 'token-123',
			body: { sourceUrl: 'https://example.com/recipe' }
		});

		const response = await POST(event);

		expect(response.status).toBe(200);
		expect(await response.json()).toEqual(imported);
		expect(importRecipeIngredients).toHaveBeenCalledWith(
			'token-123',
			{ sourceUrl: 'https://example.com/recipe' },
			event.fetch
		);
	});

	it('passes through ApiError status and message', async () => {
		vi.mocked(importRecipeIngredients).mockRejectedValue(
			new ApiError(400, 'Source URL is required.')
		);

		const event = createEvent({
			accessToken: 'token-123',
			body: { sourceUrl: '' }
		});

		const response = await POST(event);

		expect(response.status).toBe(400);
		expect(await response.json()).toEqual({ error: 'Source URL is required.' });
	});
});
