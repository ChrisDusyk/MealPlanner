import { redirect } from '@sveltejs/kit';
import type { RequestHandler } from './$types';

function trimTrailingSlash(value: string): string {
	return value.replace(/\/$/, '');
}

export const GET: RequestHandler = async ({ url }) => {
	const issuer = process.env.AUTH_AUTH0_ISSUER;
	const clientId = process.env.AUTH_AUTH0_ID;

	if (!issuer || !clientId) {
		throw redirect(302, '/');
	}

	const auth0LogoutUrl = new URL(`${trimTrailingSlash(issuer)}/v2/logout`);
	auth0LogoutUrl.searchParams.set('client_id', clientId);
	auth0LogoutUrl.searchParams.set('returnTo', new URL('/', url.origin).toString());

	throw redirect(302, auth0LogoutUrl.toString());
};
