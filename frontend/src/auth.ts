import { SvelteKitAuth } from '@auth/sveltekit';
import Auth0 from '@auth/sveltekit/providers/auth0';

interface SyncUserPayload {
	name: string;
	email?: string;
}

function normalizeBaseUrl(value?: string, fallbackPort?: string): string {
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
	} catch {
		return '';
	}
}

function getApiBaseUrl(): string {
	const explicitApiUrl = normalizeBaseUrl(
		process.env.API_INTERNAL_URL || process.env.API_BASE_URL,
		process.env.API_PORT
	);
	if (explicitApiUrl) return explicitApiUrl;

	return normalizeBaseUrl(process.env.services__api__https__0 || process.env.services__api__http__0);
}

function getStringValue(source: Record<string, unknown> | undefined, key: string): string | undefined {
	const value = source?.[key];
	return typeof value === 'string' && value.trim().length > 0 ? value.trim() : undefined;
}

function buildSyncUserPayload(token: Record<string, unknown>, profile?: Record<string, unknown>): SyncUserPayload | null {
	const auth0UserId =
		getStringValue(profile, 'sub') ||
		getStringValue(token, 'sub') ||
		'';

	if (!auth0UserId) {
		return null;
	}

	const email = getStringValue(profile, 'email') || getStringValue(token, 'email');
	const name =
		getStringValue(profile, 'name') ||
		getStringValue(token, 'name') ||
		email ||
		auth0UserId;

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
					try {
						await syncUserWithApi(accessToken, syncPayload);
					} catch (error) {
						console.error('Failed to sync user to API during authentication callback.', error);
					}
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
