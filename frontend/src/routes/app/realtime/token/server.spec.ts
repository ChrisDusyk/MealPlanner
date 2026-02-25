import { describe, expect, it, vi } from 'vitest';
import type { RequestEvent } from '@sveltejs/kit';
import { GET } from './+server';

function createEvent(accessToken: string | null): RequestEvent {
	return {
		locals: {
			auth: vi.fn().mockResolvedValue(accessToken ? { accessToken } : null)
		}
	} as unknown as RequestEvent;
}

describe('GET /app/realtime/token', () => {
	it('returns 401 when session has no access token', async () => {
		const event = createEvent(null);
		await expect(GET(event)).rejects.toMatchObject({ status: 401 });
	});

	it('returns access token with no-store cache header', async () => {
		const event = createEvent('test-token');
		const response = await GET(event);

		expect(response.status).toBe(200);
		expect(response.headers.get('Cache-Control')).toBe('no-store');
		expect(await response.json()).toEqual({ accessToken: 'test-token' });
	});
});
