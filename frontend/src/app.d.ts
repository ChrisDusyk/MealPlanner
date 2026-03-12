// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces

import '@auth/sveltekit';

declare module '@auth/sveltekit' {
	interface Session {
		accessToken: string;
		error?: 'RefreshAccessTokenError';
	}
}

declare module '@auth/core/jwt' {
	interface JWT {
		accessToken?: string;
		accessTokenExpires?: number;
		refreshToken?: string;
		idToken?: string;
		error?: 'RefreshAccessTokenError';
	}
}

declare global {
	namespace App {
		// interface Error {}
		// interface Locals {}
		// interface PageData {}
		// interface PageState {}
		// interface Platform {}
	}
}

export {};
