import { describe, expect, it, vi } from 'vitest';
import { load } from './+layout.server';

function createEvent(
	session: Record<string, unknown> | null,
	options: {
		appUser?: { onboardingCompletedAt?: string | null } | null;
		pathname?: string;
	} = {}
) {
	return {
		locals: {
			auth: vi.fn().mockResolvedValue(session)
		},
		parent: vi.fn().mockResolvedValue({
			appUser: options.appUser ?? null
		}),
		url: new URL(`http://localhost${options.pathname ?? '/app'}`)
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
			location: '/?session=expired'
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
		const event = createEvent(session, {
			appUser: { onboardingCompletedAt: '2026-01-01T00:00:00Z' }
		});
		const result = await load(event);

		expect(result).toEqual({ session });
	});

	it('redirects to onboarding when the app user has not completed it yet', async () => {
		const session = { user: { name: 'Pat' }, accessToken: 'token' };
		const event = createEvent(session, {
			appUser: { onboardingCompletedAt: null },
			pathname: '/app'
		});

		await expect(load(event)).rejects.toMatchObject({
			status: 303,
			location: '/app/onboarding'
		});
	});

	it('does not redirect when already on the onboarding route', async () => {
		const session = { user: { name: 'Pat' }, accessToken: 'token' };
		const event = createEvent(session, {
			appUser: { onboardingCompletedAt: null },
			pathname: '/app/onboarding'
		});

		const result = await load(event);
		expect(result).toEqual({ session });
	});
});
