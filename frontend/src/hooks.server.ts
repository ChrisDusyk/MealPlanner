import { building } from '$app/environment';
import { svelteKitHandler } from 'better-auth/svelte-kit';
import type { Handle } from '@sveltejs/kit';
import { auth } from '$lib/server/auth';
import { resolveAppSession } from '$lib/server/session';
import type { AppSession } from '$lib/auth/session';

/**
 * Populate `event.locals.auth` with a memoised async accessor that returns the
 * app's session shape (user + Better Auth JWT + roles). Preserves the contract
 * the codebase used with Auth.js so existing `+page.server.ts` / `+server.ts`
 * handlers keep working without per-file changes.
 */
export const handle: Handle = async ({ event, resolve }) => {
	let cached: AppSession | null | undefined;
	event.locals.auth = async () => {
		if (cached !== undefined) return cached;
		cached = await resolveAppSession(event.request);
		return cached;
	};

	return svelteKitHandler({ event, resolve, auth, building });
};
