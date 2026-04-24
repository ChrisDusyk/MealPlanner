import { sveltekitCookies } from 'better-auth/svelte-kit';
import { getRequestEvent } from '$app/server';
import { createAuth } from './auth-options';

export const auth = createAuth([sveltekitCookies(getRequestEvent)]);
