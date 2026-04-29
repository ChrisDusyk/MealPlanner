import { beforeEach, describe, expect, it, vi } from 'vitest';
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
	beforeEach(() => {
		betterAuthMock.mockClear();
		delete process.env.GOOGLE_CLIENT_ID;
		delete process.env.GOOGLE_CLIENT_SECRET;
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
});
