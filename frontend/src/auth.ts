import { SvelteKitAuth } from '@auth/sveltekit';
import Auth0 from '@auth/sveltekit/providers/auth0';

interface SyncUserPayload {
	name: string;
	email?: string;
}

const USER_SYNC_MAX_ATTEMPTS = 3;
const USER_SYNC_BASE_RETRY_DELAY_MS = 250;
const ACCESS_TOKEN_REFRESH_BUFFER_MS = 5 * 60 * 1000;
const DEFAULT_ACCESS_TOKEN_LIFETIME_SECONDS = 60 * 60;

interface RefreshTokenResponse {
	access_token: string;
	expires_in: number;
	refresh_token?: string;
	id_token?: string;
}

function normalizeBaseUrl(source: string, value?: string, fallbackPort?: string): string {
	if (!value) return '';

	const trimmed = value.trim();
	if (!trimmed) return '';

	const withProtocol = /^https?:\/\//i.test(trimmed) ? trimmed : `http://${trimmed}`;

	try {
		const parsed = new URL(withProtocol);
		if (!parsed.port && fallbackPort) {
			parsed.port = fallbackPort;
		}
		return parsed.toString().replace(/\/$/, '');
	} catch (error) {
		console.warn('Invalid API base URL value for user sync.', {
			source,
			error
		});
		return '';
	}
}

function getApiBaseUrl(): string {
	const explicitApiUrl = normalizeBaseUrl(
		'API_INTERNAL_URL/API_BASE_URL',
		process.env.API_INTERNAL_URL || process.env.API_BASE_URL,
		process.env.API_PORT
	);
	if (explicitApiUrl) return explicitApiUrl;

	const serviceDiscoveryUrl = normalizeBaseUrl(
		'services__api__https__0/services__api__http__0',
		process.env.services__api__https__0 || process.env.services__api__http__0
	);

	if (!serviceDiscoveryUrl) {
		console.warn('Unable to resolve API base URL for user sync. Skipping sync for this request.');
	}

	return serviceDiscoveryUrl;
}

function getStringValue(source: Record<string, unknown> | undefined, key: string): string | undefined {
	const value = source?.[key];
	return typeof value === 'string' && value.trim().length > 0 ? value.trim() : undefined;
}

function buildSyncUserPayload(token: Record<string, unknown>, profile?: Record<string, unknown>): SyncUserPayload | null {
	const email = getStringValue(profile, 'email') || getStringValue(token, 'email');
	const name =
		getStringValue(profile, 'name') ||
		getStringValue(token, 'name') ||
		email ||
		'Simple Meal Planner User';

	return {
		name,
		email
	};
}

async function syncUserWithApi(accessToken: string, payload: SyncUserPayload): Promise<void> {
	const apiBaseUrl = getApiBaseUrl();
	if (!apiBaseUrl) {
		return;
	}

	const response = await fetch(`${apiBaseUrl}/api/users/sync`, {
		method: 'POST',
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: JSON.stringify(payload)
	});

	if (!response.ok) {
		const details = await response.text();
		throw new Error(`User sync failed with status ${response.status}: ${details}`);
	}
}

function wait(delayMs: number): Promise<void> {
	return new Promise((resolve) => {
		setTimeout(resolve, delayMs);
	});
}

async function syncUserWithRetry(accessToken: string, payload: SyncUserPayload): Promise<void> {
	let lastError: unknown;

	for (let attempt = 1; attempt <= USER_SYNC_MAX_ATTEMPTS; attempt++) {
		try {
			await syncUserWithApi(accessToken, payload);
			if (attempt > 1) {
				console.info('User sync succeeded after retry.', { attempt });
			}
			return;
		} catch (error) {
			lastError = error;
			if (attempt < USER_SYNC_MAX_ATTEMPTS) {
				const retryDelayMs = USER_SYNC_BASE_RETRY_DELAY_MS * 2 ** (attempt - 1);
				console.warn('User sync failed. Retrying.', { attempt, retryDelayMs, error });
				await wait(retryDelayMs);
				continue;
			}
		}
	}

	console.error('User sync failed after all retry attempts.', { error: lastError });
}

function resolveAccessTokenExpiry(account: Record<string, unknown>): number {
	if (typeof account.expires_at === 'number' && Number.isFinite(account.expires_at)) {
		return account.expires_at * 1000;
	}

	if (typeof account.expires_in === 'number' && Number.isFinite(account.expires_in)) {
		return Date.now() + account.expires_in * 1000;
	}

	return Date.now() + DEFAULT_ACCESS_TOKEN_LIFETIME_SECONDS * 1000;
}

function hasValidAccessToken(token: Record<string, unknown>): boolean {
	if (typeof token.accessToken !== 'string' || token.accessToken.length === 0) {
		return false;
	}

	if (typeof token.accessTokenExpires !== 'number' || !Number.isFinite(token.accessTokenExpires)) {
		return false;
	}

	return Date.now() < token.accessTokenExpires - ACCESS_TOKEN_REFRESH_BUFFER_MS;
}

async function refreshAccessToken(token: Record<string, unknown>): Promise<Record<string, unknown>> {
	const refreshToken = typeof token.refreshToken === 'string' ? token.refreshToken : '';
	if (!refreshToken) {
		console.warn('No refresh token available. User re-authentication is required.');
		return {
			...token,
			error: 'RefreshAccessTokenError',
			accessToken: ''
		};
	}

	const issuer = process.env.AUTH_AUTH0_ISSUER?.replace(/\/$/, '');
	if (!issuer || !process.env.AUTH_AUTH0_ID || !process.env.AUTH_AUTH0_SECRET) {
		console.error('Missing Auth0 refresh configuration. User re-authentication is required.');
		return {
			...token,
			error: 'RefreshAccessTokenError',
			accessToken: ''
		};
	}

	try {
		const body = new URLSearchParams({
			grant_type: 'refresh_token',
			client_id: process.env.AUTH_AUTH0_ID,
			client_secret: process.env.AUTH_AUTH0_SECRET,
			refresh_token: refreshToken
		});

		const response = await fetch(`${issuer}/oauth/token`, {
			method: 'POST',
			headers: {
				'Content-Type': 'application/x-www-form-urlencoded'
			},
			body
		});

		if (!response.ok) {
			const details = await response.text();
			throw new Error(`Token refresh failed with status ${response.status}: ${details}`);
		}

		const refreshed = (await response.json()) as RefreshTokenResponse;
		return {
			...token,
			accessToken: refreshed.access_token,
			accessTokenExpires: Date.now() + refreshed.expires_in * 1000,
			refreshToken: refreshed.refresh_token ?? refreshToken,
			idToken: refreshed.id_token ?? token.idToken,
			error: undefined
		};
	} catch (error) {
		console.error('Failed to refresh access token.', { error });
		return {
			...token,
			error: 'RefreshAccessTokenError',
			accessToken: ''
		};
	}
}

export const { handle, signIn, signOut } = SvelteKitAuth({
	trustHost: true,
	providers: [
		Auth0({
			clientId: process.env.AUTH_AUTH0_ID,
			clientSecret: process.env.AUTH_AUTH0_SECRET,
			issuer: process.env.AUTH_AUTH0_ISSUER,
			authorization: {
				params: {
					audience: process.env.AUTH_API_AUDIENCE,
					scope: 'openid profile email offline_access'
				}
			}
		})
	],
	callbacks: {
		async jwt({ token, account, profile }) {
			const tokenRecord = token as Record<string, unknown>;

			// Persist the access token from the Auth0 provider to the JWT
			if (account) {
				tokenRecord.accessToken = account.access_token;
				tokenRecord.accessTokenExpires = resolveAccessTokenExpiry(account as Record<string, unknown>);
				tokenRecord.refreshToken =
					typeof account.refresh_token === 'string' ? account.refresh_token : tokenRecord.refreshToken;
				tokenRecord.idToken = account.id_token;
				tokenRecord.error = undefined;

				const accessToken = typeof account.access_token === 'string' ? account.access_token : '';
				const syncPayload = buildSyncUserPayload(
					tokenRecord,
					profile as Record<string, unknown> | undefined
				);

				if (accessToken && syncPayload) {
					await syncUserWithRetry(accessToken, syncPayload);
				} else {
					console.warn('Skipping user sync due to missing access token or profile payload.');
				}

				return token;
			}

			if (hasValidAccessToken(tokenRecord)) {
				return token;
			}

			const refreshedToken = await refreshAccessToken(tokenRecord);
			return refreshedToken;
		},
		async session({ session, token }) {
			// Make the access token available in the session for API calls
			session.accessToken = typeof token.accessToken === 'string' ? token.accessToken : '';
			session.error = token.error === 'RefreshAccessTokenError' ? 'RefreshAccessTokenError' : undefined;
			return session;
		}
	}
});
