import { SvelteKitAuth } from '@auth/sveltekit';
import Keycloak from '@auth/sveltekit/providers/keycloak';

export const { handle, signIn, signOut } = SvelteKitAuth({
	trustHost: true,
	providers: [
		Keycloak({
			clientId: process.env.AUTH_KEYCLOAK_ID ?? 'mealplanner-web',
			clientSecret: process.env.AUTH_KEYCLOAK_SECRET ?? 'mealplanner-web-secret',
			issuer: process.env.AUTH_KEYCLOAK_ISSUER ?? 'http://localhost:8080/realms/mealplanner'
		})
	],
	callbacks: {
		async jwt({ token, account }) {
			// Persist the access token from the Keycloak provider to the JWT
			if (account) {
				token.accessToken = account.access_token;
				token.idToken = account.id_token;
			}
			return token;
		},
		async session({ session, token }) {
			// Make the access token available in the session for API calls
			session.accessToken = typeof token.accessToken === 'string' ? token.accessToken : '';
			return session;
		}
	}
});
