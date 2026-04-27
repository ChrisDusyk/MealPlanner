import { sveltekitCookies } from 'better-auth/svelte-kit';
import { getRequestEvent } from '$app/server';
import { building } from '$app/environment';
import { createAuth } from './auth-options';
import { dash } from '@better-auth/infra';

export const auth = createAuth([sveltekitCookies(getRequestEvent), dash()], {
	allowMissingConnectionString: building,
});
