import { SvelteKitAuth } from '@auth/sveltekit';
import Auth0 from '@auth/sveltekit/providers/auth0';

interface SyncUserPayload {
	name: string;
	email?: string;
}

const USER_SYNC_MAX_ATTEMPTS = 3;
const USER_SYNC_BASE_RETRY_DELAY_MS = 250;

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
		'MealPlanner User';

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

export const { handle, signIn, signOut } = SvelteKitAuth({
	trustHost: true,
	providers: [
		Auth0({
			clientId: process.env.AUTH_AUTH0_ID,
			clientSecret: process.env.AUTH_AUTH0_SECRET,
			issuer: process.env.AUTH_AUTH0_ISSUER,
			authorization: {
				params: {
					audience: process.env.AUTH_API_AUDIENCE
				}
			}
		})
	],
	callbacks: {
		async jwt({ token, account, profile }) {
			// Persist the access token from the Auth0 provider to the JWT
			if (account) {
				token.accessToken = account.access_token;
				token.idToken = account.id_token;

				const accessToken = typeof account.access_token === 'string' ? account.access_token : '';
				const syncPayload = buildSyncUserPayload(
					token as Record<string, unknown>,
					profile as Record<string, unknown> | undefined
				);

				if (accessToken && syncPayload) {
					await syncUserWithRetry(accessToken, syncPayload);
				} else {
					console.warn('Skipping user sync due to missing access token or profile payload.');
				}
			}
			return token;
		},
		async session({ session, token }) {
			// Make the access token available in the session for API calls
			session.accessToken = typeof token.accessToken === 'string' ? token.accessToken : '';
			return session;
		}
	}
});
