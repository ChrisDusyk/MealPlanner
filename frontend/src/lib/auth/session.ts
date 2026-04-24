/**
 * Shared session shape used across the SvelteKit frontend.
 *
 * Server-only helpers that produce these sessions live in
 * `$lib/server/session` — this module is safe to import from browser code
 * for typing alone.
 */

export type AppRoleString = string;

export interface AppSessionUser {
	id: string;
	email: string | null;
	name: string | null;
	image?: string | null;
	role?: AppRoleString;
	displayName?: string | null;
	timezone?: string | null;
	onboardingCompletedAt?: string | null;
}

export interface AppSession {
	user: AppSessionUser;
	accessToken: string;
	roles: string[];
	error?: 'RefreshAccessTokenError';
}
