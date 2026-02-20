import { SvelteKitAuth } from '@auth/sveltekit';
import Auth0 from '@auth/sveltekit/providers/auth0';

console.log(process.env);

export const { handle, signIn, signOut } = SvelteKitAuth({
	trustHost: true,
	providers: [
		Auth0({
			clientId: process.env.AUTH_AUTH0_ID,
			clientSecret: process.env.AUTH_AUTH0_SECRET,
			issuer: process.env.AUTH_AUTH0_ISSUER,
			authorization: {
				params: {
					audience: process.env.AUTH_API_AUDIENCE
				}
			}
		})
	],
	callbacks: {
		async jwt({ token, account }) {
			// Persist the access token from the Auth0 provider to the JWT
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
