import { betterAuth } from 'better-auth';
import { sveltekitCookies } from 'better-auth/svelte-kit';
import { getRequestEvent } from '$app/server';
import { createBaseAuthOptions } from './auth-options';

const baseOptions = createBaseAuthOptions();

export const auth = betterAuth({
	...baseOptions,
	plugins: [
		...(baseOptions.plugins ?? []),
		// `sveltekitCookies` must be the last plugin per Better Auth docs.
		sveltekitCookies(getRequestEvent)
	]
});
