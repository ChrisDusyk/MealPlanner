import { describe, expect, it, vi } from 'vitest';
import { load } from './+layout.server';

function createEvent(session: Record<string, unknown> | null) {
	return {
		locals: {
			auth: vi.fn().mockResolvedValue(session)
		}
	} as unknown as Parameters<typeof load>[0];
}

describe('app layout auth guard', () => {
	it('redirects to home when refresh token flow failed', async () => {
		const event = createEvent({
			user: { name: 'Pat' },
			error: 'RefreshAccessTokenError'
		});

		await expect(load(event)).rejects.toMatchObject({
			status: 303,
			location: '/'
		});
	});

	it('redirects to home when session has no user', async () => {
		const event = createEvent(null);

		await expect(load(event)).rejects.toMatchObject({
			status: 303,
			location: '/'
		});
	});

	it('returns session for authenticated users', async () => {
		const session = {
			user: { name: 'Pat' },
			accessToken: 'token'
		};
		const event = createEvent(session);
		const result = await load(event);

		expect(result).toEqual({ session });
	});
});
