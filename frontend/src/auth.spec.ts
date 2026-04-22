import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest';

const svelteKitAuthMock = vi.fn((config: unknown) => ({
	handle: vi.fn(),
	signIn: vi.fn(),
	signOut: vi.fn(),
	config
}));

vi.mock('@auth/sveltekit', () => ({
	SvelteKitAuth: svelteKitAuthMock
}));

vi.mock('@auth/sveltekit/providers/auth0', () => ({
	default: vi.fn((config: unknown) => config)
}));

function createJsonResponse(payload: unknown, status = 200): Response {
	return new Response(JSON.stringify(payload), {
		status,
		headers: { 'Content-Type': 'application/json' }
	});
}

function createAccessToken(payload: Record<string, unknown>): string {
	const header = Buffer.from(JSON.stringify({ alg: 'none', typ: 'JWT' })).toString('base64url');
	const body = Buffer.from(JSON.stringify(payload)).toString('base64url');
	return `${header}.${body}.sig`;
}

async function loadAuthCallbacks() {
	vi.resetModules();
	svelteKitAuthMock.mockClear();

	process.env.AUTH_AUTH0_ID = 'test-client-id';
	process.env.AUTH_AUTH0_SECRET = 'test-client-secret';
	process.env.AUTH_AUTH0_ISSUER = 'https://tenant.example.com/';
	process.env.AUTH_API_AUDIENCE = 'https://api.example.com';
	process.env.API_BASE_URL = 'http://api.local';

	await import('./auth');

	const authConfig = svelteKitAuthMock.mock.calls[0]?.[0] as {
		callbacks: {
			jwt: (args: Record<string, unknown>) => Promise<Record<string, unknown>>;
			session: (args: Record<string, unknown>) => Promise<Record<string, unknown>>;
		};
	};

	if (!authConfig?.callbacks) {
		throw new Error('Unable to capture Auth.js callbacks in test.');
	}

	return authConfig.callbacks;
}

describe('auth callbacks', () => {
	beforeEach(() => {
		vi.clearAllMocks();
		vi.restoreAllMocks();
		global.fetch = vi.fn();
	});

	it('captures access, refresh, and expiry metadata on sign-in', async () => {
		const accessToken = createAccessToken({
			'https://mealplanner/roles': ['user']
		});
		(global.fetch as Mock).mockResolvedValue(createJsonResponse({ ok: true }));
		const callbacks = await loadAuthCallbacks();

		const token = await callbacks.jwt({
			token: {},
			account: {
				access_token: accessToken,
				refresh_token: 'initial-refresh',
				id_token: 'id-token',
				expires_at: 1_800_000_000
			},
			profile: {
				name: 'Pat Example',
				email: 'pat@example.com'
			}
		});

		expect(token.accessToken).toBe(accessToken);
		expect(token.refreshToken).toBe('initial-refresh');
		expect(token.idToken).toBe('id-token');
		expect(token.accessTokenExpires).toBe(1_800_000_000_000);
		expect(token.roles).toEqual(['user']);
		expect(global.fetch).toHaveBeenCalledWith(
			'http://api.local/api/users/sync',
			expect.objectContaining({ method: 'POST' })
		);
	});

	it('reuses token when it is not close to expiration', async () => {
		const now = 1_800_000_000_000;
		vi.spyOn(Date, 'now').mockReturnValue(now);
		const callbacks = await loadAuthCallbacks();

		const token = await callbacks.jwt({
			token: {
				accessToken: 'still-valid',
				accessTokenExpires: now + 10 * 60 * 1000,
				refreshToken: 'refresh-token'
			},
			account: undefined,
			profile: undefined
		});

		expect(token.accessToken).toBe('still-valid');
		expect(global.fetch).not.toHaveBeenCalled();
	});

	it('refreshes token when access token is expired', async () => {
		const refreshedAccessToken = createAccessToken({
			'https://mealplanner/roles': ['admin']
		});
		const now = 1_800_000_000_000;
		vi.spyOn(Date, 'now').mockReturnValue(now);
		(global.fetch as Mock).mockResolvedValue(
			createJsonResponse({
				access_token: refreshedAccessToken,
				expires_in: 3600,
				refresh_token: 'rotated-refresh',
				id_token: 'updated-id-token'
			})
		);
		const callbacks = await loadAuthCallbacks();

		const token = await callbacks.jwt({
			token: {
				accessToken: 'expired-access',
				accessTokenExpires: now - 1,
				refreshToken: 'old-refresh',
				idToken: 'old-id-token'
			},
			account: undefined,
			profile: undefined
		});

		expect(global.fetch).toHaveBeenCalledWith(
			'https://tenant.example.com/oauth/token',
			expect.objectContaining({ method: 'POST' })
		);
		const refreshRequest = (global.fetch as Mock).mock.calls[0]?.[1] as {
			body: URLSearchParams;
		};
		expect(refreshRequest.body.toString()).toContain('grant_type=refresh_token');
		expect(token.accessToken).toBe(refreshedAccessToken);
		expect(token.refreshToken).toBe('rotated-refresh');
		expect(token.idToken).toBe('updated-id-token');
		expect(token.roles).toEqual(['admin']);
		expect(token.error).toBeUndefined();
	});

	it('flags refresh failure when Auth0 refresh fails', async () => {
		const now = 1_800_000_000_000;
		vi.spyOn(Date, 'now').mockReturnValue(now);
		(global.fetch as Mock).mockResolvedValue(
			new Response('expired refresh token', { status: 401 })
		);
		const callbacks = await loadAuthCallbacks();

		const token = await callbacks.jwt({
			token: {
				accessToken: 'expired-access',
				accessTokenExpires: now - 1,
				refreshToken: 'expired-refresh'
			},
			account: undefined,
			profile: undefined
		});

		expect(token.accessToken).toBe('');
		expect(token.error).toBe('RefreshAccessTokenError');
	});

	it('propagates refresh failure marker to session', async () => {
		const callbacks = await loadAuthCallbacks();
		const session = await callbacks.session({
			session: {
				user: { name: 'Pat' }
			},
			token: {
				accessToken: '',
				error: 'RefreshAccessTokenError'
			}
		});

		expect(session).toMatchObject({
			accessToken: '',
			error: 'RefreshAccessTokenError'
		});
	});

	it('propagates token roles to session', async () => {
		const callbacks = await loadAuthCallbacks();
		const session = await callbacks.session({
			session: {
				user: { name: 'Pat' }
			},
			token: {
				accessToken: 'token',
				roles: ['admin']
			}
		});

		expect(session).toMatchObject({
			accessToken: 'token',
			roles: ['admin']
		});
	});
});
