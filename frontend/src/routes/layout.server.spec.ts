import { beforeEach, describe, expect, it, vi } from 'vitest';
import { load } from './+layout.server';
import { getCurrentUser, syncCurrentUser } from '$lib/api/userApi';
import { ApiError } from '$lib/api/apiHelpers';

vi.mock('$lib/api/userApi', () => ({
	getCurrentUser: vi.fn(),
	syncCurrentUser: vi.fn()
}));

vi.mock('$lib/server/featureFlags', () => ({
	getServerFlags: vi.fn().mockResolvedValue({ demoBanner: false })
}));

type RootLayoutLoadEvent = Parameters<typeof load>[0];

function createEvent(
	session: {
		user?: { name?: string | null; email?: string | null };
		accessToken?: string;
	} | null
): RootLayoutLoadEvent {
	return {
		locals: {
			auth: vi.fn().mockResolvedValue(session)
		},
		fetch: vi.fn()
	} as unknown as RootLayoutLoadEvent;
}

describe('root layout user sync', () => {
	beforeEach(() => {
		vi.clearAllMocks();
	});

	it('returns current user when already present in app database', async () => {
		const appUser = {
			id: 'u1',
			authUserId: 'better-auth|123',
			name: 'Pat',
			email: 'pat@example.com',
			displayName: null,
			timezone: null,
			onboardingCompletedAt: null,
			createdAt: '2026-01-01T00:00:00Z',
			updatedAt: '2026-01-01T00:00:00Z'
		};
		vi.mocked(getCurrentUser).mockResolvedValue(appUser);

		const event = createEvent({
			user: { name: 'Pat', email: 'pat@example.com' },
			accessToken: 'test-token'
		});
		const result = await load(event);

		expect(result).toEqual({
			session: { user: { name: 'Pat', email: 'pat@example.com' }, accessToken: 'test-token' },
			appUser,
			flags: { demoBanner: false }
		});
		expect(syncCurrentUser).not.toHaveBeenCalled();
	});

	it('syncs user when /api/users/me returns 404', async () => {
		vi.mocked(getCurrentUser).mockRejectedValue(new ApiError(404, 'User was not found.'));
		const syncedUser = {
			id: 'u1',
			authUserId: 'better-auth|123',
			name: 'Pat',
			email: 'pat@example.com',
			displayName: null,
			timezone: null,
			onboardingCompletedAt: null,
			createdAt: '2026-01-01T00:00:00Z',
			updatedAt: '2026-01-01T00:00:00Z'
		};
		vi.mocked(syncCurrentUser).mockResolvedValue(syncedUser);

		const event = createEvent({
			user: { name: 'Pat', email: 'pat@example.com' },
			accessToken: 'test-token'
		});
		const result = await load(event);

		expect(syncCurrentUser).toHaveBeenCalledWith(
			'test-token',
			{ name: 'Pat', email: 'pat@example.com' },
			event.fetch
		);
		expect(result).toEqual({
			session: { user: { name: 'Pat', email: 'pat@example.com' }, accessToken: 'test-token' },
			appUser: syncedUser,
			flags: { demoBanner: false }
		});
	});

	it('keeps request alive when sync fails after 404', async () => {
		vi.mocked(getCurrentUser).mockRejectedValue(new ApiError(404, 'User was not found.'));
		vi.mocked(syncCurrentUser).mockRejectedValue(new ApiError(500, 'Sync failed'));
		const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});

		const event = createEvent({
			user: { name: 'Pat', email: 'pat@example.com' },
			accessToken: 'test-token'
		});
		const result = await load(event);

		expect(result).toEqual({
			session: { user: { name: 'Pat', email: 'pat@example.com' }, accessToken: 'test-token' },
			appUser: null,
			flags: { demoBanner: false }
		});
		expect(warn).toHaveBeenCalled();
		warn.mockRestore();
	});
});
