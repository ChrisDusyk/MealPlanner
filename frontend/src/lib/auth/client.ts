import { createAuthClient } from 'better-auth/svelte';
import { adminClient, inferAdditionalFields } from 'better-auth/client/plugins';
import type { AuthInstance } from '$lib/server/auth';

export const authClient = createAuthClient({
	plugins: [adminClient(), inferAdditionalFields<AuthInstance>()]
});

export const { signIn, signUp, signOut, useSession, getSession } = authClient;
