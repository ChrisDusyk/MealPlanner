import { building } from '$app/environment';
import { svelteKitHandler } from 'better-auth/svelte-kit';
import { redirect, type Handle } from '@sveltejs/kit';
import { sequence } from '@sveltejs/kit/hooks';
import { auth } from '$lib/server/auth';
import { resolveAppSession } from '$lib/server/session';
import type { AppSession } from '$lib/auth/session';

/**
 * Canonicalise the host by redirecting the `www.` subdomain to the apex
 * domain. Railway serves both `www.simplemealplanner.ca` and the apex from the
 * same service; consolidating on a single origin keeps session cookies, CORS,
 * and Better Auth trusted-origin checks pointed at one host.
 *
 * The client host is read from `x-forwarded-host` because adapter-node runs
 * behind Railway's proxy (and may have `ORIGIN` pinned to the apex, which would
 * otherwise mask the real request host on `event.url`).
 */
const canonicalHost: Handle = async ({ event, resolve }) => {
	const forwardedHost = event.request.headers.get('x-forwarded-host');
	const host = (forwardedHost ?? event.url.host).split(',')[0].trim().toLowerCase();

	if (host.startsWith('www.')) {
		const apexHost = host.slice(4);
		// 308 (not 301) preserves the request method, so in-flight POST auth
		// calls that land on www are replayed against the apex host.
		redirect(308, `https://${apexHost}${event.url.pathname}${event.url.search}`);
	}

	return resolve(event);
};

/**
 * Populate `event.locals.auth` with a memoised async accessor that returns the
 * app's session shape (user + Better Auth JWT + roles). Preserves the contract
 * the codebase used with Auth.js so existing `+page.server.ts` / `+server.ts`
 * handlers keep working without per-file changes.
 */
const authHandle: Handle = async ({ event, resolve }) => {
	let cached: AppSession | null | undefined;
	event.locals.auth = async () => {
		if (cached !== undefined) return cached;
		cached = await resolveAppSession(event.request);
		return cached;
	};

	return svelteKitHandler({ event, resolve, auth, building });
};

export const handle = sequence(canonicalHost, authHandle);
