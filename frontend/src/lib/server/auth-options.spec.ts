import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createAuth } from './auth-options';

const betterAuthMock = vi.fn((options: unknown) => options);

vi.mock('better-auth', () => ({
	betterAuth: (options: unknown) => betterAuthMock(options)
}));

vi.mock('better-auth/plugins', () => ({
	admin: () => ({ name: 'admin' }),
	jwt: () => ({ name: 'jwt' })
}));

describe('createAuth social provider configuration', () => {
	const originalGoogleClientId = process.env.GOOGLE_CLIENT_ID;
	const originalGoogleClientSecret = process.env.GOOGLE_CLIENT_SECRET;

	function restoreGoogleEnv() {
		if (originalGoogleClientId === undefined) {
			delete process.env.GOOGLE_CLIENT_ID;
		} else {
			process.env.GOOGLE_CLIENT_ID = originalGoogleClientId;
		}

		if (originalGoogleClientSecret === undefined) {
			delete process.env.GOOGLE_CLIENT_SECRET;
		} else {
			process.env.GOOGLE_CLIENT_SECRET = originalGoogleClientSecret;
		}
	}

	beforeEach(() => {
		betterAuthMock.mockClear();
		delete process.env.GOOGLE_CLIENT_ID;
		delete process.env.GOOGLE_CLIENT_SECRET;
	});

	afterEach(() => {
		restoreGoogleEnv();
	});

	it('configures Google provider when both credentials are set', () => {
		process.env.GOOGLE_CLIENT_ID = '  google-client-id  ';
		process.env.GOOGLE_CLIENT_SECRET = '  google-client-secret  ';

		createAuth([], { allowMissingConnectionString: true });

		expect(betterAuthMock).toHaveBeenCalledOnce();
		expect(betterAuthMock.mock.calls[0]?.[0]).toMatchObject({
			socialProviders: {
				google: {
					clientId: 'google-client-id',
					clientSecret: 'google-client-secret'
				}
			}
		});
	});

	it('does not configure Google provider when credentials are missing', () => {
		createAuth([], { allowMissingConnectionString: true });

		expect(betterAuthMock).toHaveBeenCalledOnce();
		expect(betterAuthMock.mock.calls[0]?.[0]).toMatchObject({
			socialProviders: undefined
		});
	});

	it('does not configure Google provider when only client id is set', () => {
		process.env.GOOGLE_CLIENT_ID = 'google-client-id';

		createAuth([], { allowMissingConnectionString: true });

		expect(betterAuthMock).toHaveBeenCalledOnce();
		expect(betterAuthMock.mock.calls[0]?.[0]).toMatchObject({
			socialProviders: undefined
		});
	});

	it('does not configure Google provider when only client secret is set', () => {
		process.env.GOOGLE_CLIENT_SECRET = 'google-client-secret';

		createAuth([], { allowMissingConnectionString: true });

		expect(betterAuthMock).toHaveBeenCalledOnce();
		expect(betterAuthMock.mock.calls[0]?.[0]).toMatchObject({
			socialProviders: undefined
		});
	});
});

describe('createAuth cross-subdomain cookie configuration', () => {
	const originalAuthUrl = process.env.BETTER_AUTH_URL;
	const originalCookieDomain = process.env.BETTER_AUTH_COOKIE_DOMAIN;

	function restoreEnv(key: string, value: string | undefined) {
		if (value === undefined) {
			delete process.env[key];
		} else {
			process.env[key] = value;
		}
	}

	beforeEach(() => {
		betterAuthMock.mockClear();
		delete process.env.BETTER_AUTH_URL;
		delete process.env.BETTER_AUTH_COOKIE_DOMAIN;
	});

	afterEach(() => {
		restoreEnv('BETTER_AUTH_URL', originalAuthUrl);
		restoreEnv('BETTER_AUTH_COOKIE_DOMAIN', originalCookieDomain);
	});

	function advancedFromCall() {
		return (betterAuthMock.mock.calls[0]?.[0] as { advanced?: unknown }).advanced;
	}

	it('derives a leading-dot cookie domain from an apex BETTER_AUTH_URL', () => {
		process.env.BETTER_AUTH_URL = 'https://simplemealplanner.ca';

		createAuth([], { allowMissingConnectionString: true });

		expect(advancedFromCall()).toEqual({
			crossSubDomainCookies: { enabled: true, domain: '.simplemealplanner.ca' }
		});
	});

	it('strips a www. host when deriving the cookie domain', () => {
		process.env.BETTER_AUTH_URL = 'https://www.simplemealplanner.ca';

		createAuth([], { allowMissingConnectionString: true });

		expect(advancedFromCall()).toEqual({
			crossSubDomainCookies: { enabled: true, domain: '.simplemealplanner.ca' }
		});
	});

	it('honours an explicit BETTER_AUTH_COOKIE_DOMAIN override', () => {
		process.env.BETTER_AUTH_URL = 'https://simplemealplanner.ca';
		process.env.BETTER_AUTH_COOKIE_DOMAIN = '.override.example';

		createAuth([], { allowMissingConnectionString: true });

		expect(advancedFromCall()).toEqual({
			crossSubDomainCookies: { enabled: true, domain: '.override.example' }
		});
	});

	it('does not scope cookies to a domain for localhost dev', () => {
		process.env.BETTER_AUTH_URL = 'http://localhost:3000';

		createAuth([], { allowMissingConnectionString: true });

		expect(advancedFromCall()).toBeUndefined();
	});
});
