import { redirect } from '@sveltejs/kit';
import type { RequestHandler } from './$types';

export const GET: RequestHandler = async ({ url }) => {
	const issuer = process.env.AUTH_AUTH0_ISSUER?.trim();
	const clientId = process.env.AUTH_AUTH0_ID;

	if (!issuer || !clientId) {
		throw redirect(302, '/');
	}

	let auth0LogoutUrl: URL;
	try {
		auth0LogoutUrl = new URL('/v2/logout', issuer);
	} catch {
		// If issuer is not a valid absolute URL, gracefully fall back to home.
		throw redirect(302, '/');
	}

	auth0LogoutUrl.searchParams.set('client_id', clientId);
	auth0LogoutUrl.searchParams.set('returnTo', new URL('/', url.origin).toString());

	throw redirect(302, auth0LogoutUrl.toString());
};
