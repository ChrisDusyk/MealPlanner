// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces

import '@auth/sveltekit';

declare module '@auth/sveltekit' {
	interface Session {
		accessToken: string;
	}
}

declare module '@auth/core/jwt' {
	interface JWT {
		accessToken?: string;
		idToken?: string;
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
