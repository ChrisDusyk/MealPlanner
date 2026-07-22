import { building } from '$app/environment';
import { svelteKitHandler } from 'better-auth/svelte-kit';
import { redirect, type Handle } from '@sveltejs/kit';
import { sequence } from '@sveltejs/kit/hooks';
import { auth } from '$lib/server/auth';
import { resolveCanonicalApex } from '$lib/server/auth-options';
import { resolveAppSession } from '$lib/server/session';
import type { AppSession } from '$lib/auth/session';

/**
 * Canonicalise the host by redirecting the `www.` subdomain to the apex
 * domain. Railway serves both `www.simplemealplanner.ca` and the apex from the
 * same service; consolidating on a single origin keeps session cookies, CORS,
 * and Better Auth trusted-origin checks pointed at one host.
 *
 * The observed host is read from `x-forwarded-host` because adapter-node runs
 * behind Railway's proxy (and may have `ORIGIN` pinned to the apex, which would
 * otherwise mask the real request host on `event.url`). That header is client-
 * influenceable, so it is only ever *compared* against the trusted apex derived
 * from `BETTER_AUTH_URL` — the redirect *target* comes purely from that config,
 * never from the header, which avoids an open redirect (e.g. a forged
 * `x-forwarded-host: www.evil.com` simply fails the match and is left alone).
 */
const canonicalHost: Handle = async ({ event, resolve }) => {
	const apex = resolveCanonicalApex();
	if (apex) {
		const observedHost = (event.request.headers.get('x-forwarded-host') ?? event.url.host)
			.split(',')[0]
			.trim()
			.toLowerCase();

		if (observedHost === `www.${apex.host}`) {
			// 308 (not 301) preserves the request method, so in-flight POST auth
			// calls that land on www are replayed against the apex host.
			redirect(308, `${apex.origin}${event.url.pathname}${event.url.search}`);
		}
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
