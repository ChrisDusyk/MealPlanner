import { describe, expect, it, vi } from 'vitest';
import { load } from './+page.server';

function createEvent(session: Record<string, unknown> | null) {
	return {
		locals: {
			auth: vi.fn().mockResolvedValue(session)
		}
	} as unknown as Parameters<typeof load>[0];
}

describe('admin page auth guard', () => {
	it('returns successfully for admin role', async () => {
		const event = createEvent({
			user: { name: 'Pat' },
			roles: ['admin'],
			accessToken: 'token'
		});

		await expect(load(event)).resolves.toEqual({});
	});

	it('throws forbidden for non-admin role', async () => {
		const event = createEvent({
			user: { name: 'Pat' },
			roles: ['user'],
			accessToken: 'token'
		});

		await expect(load(event)).rejects.toMatchObject({
			status: 403
		});
	});
});
