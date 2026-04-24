import { describe, expect, it, vi, beforeEach } from 'vitest';
import { actions, load } from './+page.server';
import { completeOnboarding } from '$lib/api/userApi';
import { ApiError } from '$lib/api/apiHelpers';

vi.mock('$lib/api/userApi', () => ({
	completeOnboarding: vi.fn()
}));

type LoadEvent = Parameters<typeof load>[0];
type ActionEvent = Parameters<NonNullable<typeof actions.default>>[0];

const completedUser = {
	id: 'u1',
	authUserId: 'better-auth|abc',
	name: 'Pat',
	email: 'pat@example.com',
	displayName: 'Pat H.',
	timezone: 'America/Toronto',
	onboardingCompletedAt: '2026-01-01T00:00:00Z',
	createdAt: '2026-01-01T00:00:00Z',
	updatedAt: '2026-01-01T00:00:00Z'
};

function buildAuthenticatedSession(extra: Record<string, unknown> = {}) {
	return {
		user: { id: 'u1', name: 'Pat', email: 'pat@example.com', role: 'user' },
		accessToken: 'test-token',
		roles: ['user'],
		...extra
	};
}

function createLoadEvent(
	session: Record<string, unknown> | null,
	appUser: { onboardingCompletedAt?: string | null } | null = null
): LoadEvent {
	return {
		locals: {
			auth: vi.fn().mockResolvedValue(session)
		},
		parent: vi.fn().mockResolvedValue({ appUser })
	} as unknown as LoadEvent;
}

function createActionEvent(
	session: Record<string, unknown> | null,
	formBody: Record<string, string>
): ActionEvent {
	const formData = new FormData();
	for (const [key, value] of Object.entries(formBody)) {
		formData.set(key, value);
	}
	return {
		locals: {
			auth: vi.fn().mockResolvedValue(session)
		},
		request: new Request('http://localhost/app/onboarding', {
			method: 'POST',
			body: formData
		}),
		fetch
	} as unknown as ActionEvent;
}

describe('onboarding page server load', () => {
	beforeEach(() => {
		vi.clearAllMocks();
	});

	it('redirects to home when no session', async () => {
		const event = createLoadEvent(null);
		await expect(load(event)).rejects.toMatchObject({ status: 303, location: '/' });
	});

	it('redirects to /app when onboarding already completed', async () => {
		const event = createLoadEvent(buildAuthenticatedSession(), {
			onboardingCompletedAt: '2026-01-01T00:00:00Z'
		});
		await expect(load(event)).rejects.toMatchObject({ status: 303, location: '/app' });
	});

	it('returns session and app user when onboarding pending', async () => {
		const event = createLoadEvent(buildAuthenticatedSession(), { onboardingCompletedAt: null });
		const result = await load(event);
		expect(result).toMatchObject({
			appUser: { onboardingCompletedAt: null }
		});
	});
});

describe('onboarding page server action', () => {
	beforeEach(() => {
		vi.clearAllMocks();
	});

	it('fails with 400 when display name is empty', async () => {
		const event = createActionEvent(buildAuthenticatedSession(), {
			displayName: '   ',
			timezone: 'America/Toronto'
		});
		const result = await actions.default(event);

		expect(result).toMatchObject({
			status: 400,
			data: { error: expect.stringContaining('call you') }
		});
		expect(completeOnboarding).not.toHaveBeenCalled();
	});

	it('submits trimmed profile to API and redirects on success', async () => {
		vi.mocked(completeOnboarding).mockResolvedValue(completedUser);
		const event = createActionEvent(buildAuthenticatedSession(), {
			displayName: '  Pat H.  ',
			timezone: '  America/Toronto  '
		});

		await expect(actions.default(event)).rejects.toMatchObject({
			status: 303,
			location: '/app'
		});
		expect(completeOnboarding).toHaveBeenCalledWith(
			'test-token',
			{ displayName: 'Pat H.', timezone: 'America/Toronto' },
			expect.any(Function)
		);
	});

	it('treats blank timezone as null so server can keep existing value', async () => {
		vi.mocked(completeOnboarding).mockResolvedValue(completedUser);
		const event = createActionEvent(buildAuthenticatedSession(), {
			displayName: 'Pat H.',
			timezone: ''
		});

		await expect(actions.default(event)).rejects.toMatchObject({ status: 303 });
		expect(completeOnboarding).toHaveBeenCalledWith(
			'test-token',
			{ displayName: 'Pat H.', timezone: null },
			expect.any(Function)
		);
	});

	it('surfaces API errors back to the form', async () => {
		vi.mocked(completeOnboarding).mockRejectedValue(new ApiError(409, 'User was not found.'));
		const event = createActionEvent(buildAuthenticatedSession(), {
			displayName: 'Pat H.',
			timezone: 'America/Toronto'
		});

		const result = await actions.default(event);
		expect(result).toMatchObject({
			status: 409,
			data: {
				error: 'User was not found.',
				displayName: 'Pat H.',
				timezone: 'America/Toronto'
			}
		});
	});
});
