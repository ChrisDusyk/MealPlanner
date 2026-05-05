import { json, error } from '@sveltejs/kit';
import { ApiError, fetchCatalogueTags } from '$lib/api/catalogueApi';
import { requireAuthenticatedSession } from '$lib/auth/guards';
import type { RequestHandler } from './$types';

export const GET: RequestHandler = async ({ locals, fetch }) => {
	const session = requireAuthenticatedSession(await locals.auth());
	if (!session.accessToken) {
		error(401, 'Unauthorized');
	}

	try {
		const tags = await fetchCatalogueTags(session.accessToken, fetch);
		return json(tags);
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message, body: err.body }, { status: err.status });
		}

		const message = err instanceof Error ? err.message : 'Failed to load catalogue tags.';
		return json({ error: message }, { status: 500 });
	}
};
