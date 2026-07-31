import { describe, expect, it, vi } from 'vitest';
import { load } from './+page.server';

type LoadEvent = Parameters<typeof load>[0];

function createLoadEvent(session: Record<string, unknown> | null): LoadEvent {
	return {
		locals: { auth: vi.fn().mockResolvedValue(session) }
	} as unknown as LoadEvent;
}

describe('new feature flag load', () => {
	it('allows an admin through', async () => {
		const session = { user: { id: 'u1' }, roles: ['admin'], accessToken: 'token' };

		await expect(load(createLoadEvent(session))).resolves.toEqual({});
	});

	it('throws forbidden for a non-admin', async () => {
		const session = { user: { id: 'u1' }, roles: ['user'], accessToken: 'token' };

		await expect(load(createLoadEvent(session))).rejects.toMatchObject({ status: 403 });
	});
});
