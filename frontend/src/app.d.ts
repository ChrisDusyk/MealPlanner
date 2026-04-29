// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces

import type { AppSession } from '$lib/auth/session';

declare global {
	namespace App {
		interface Locals {
			/**
			 * Memoised accessor returning the current Better Auth session enriched
			 * with a short-lived JWT (`accessToken`) for the .NET API, or `null`
			 * when the request is unauthenticated.
			 */
			auth: () => Promise<AppSession | null>;
		}
		// interface Error {}
		// interface PageData {}
		// interface PageState {}
		// interface Platform {}
	}
}

export {};
