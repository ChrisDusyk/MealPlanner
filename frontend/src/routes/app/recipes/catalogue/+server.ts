import { json, error } from '@sveltejs/kit';
import { ApiError, fetchCatalogue } from '$lib/api/catalogueApi';
import { requireAuthenticatedSession } from '$lib/auth/guards';
import type { RequestHandler } from './$types';

export const GET: RequestHandler = async ({ locals, fetch, url }) => {
	const session = requireAuthenticatedSession(await locals.auth());
	if (!session.accessToken) {
		error(401, 'Unauthorized');
	}

	const search = url.searchParams.get('search')?.trim() || undefined;
	const tagsParam = url.searchParams.get('tags')?.trim() || '';
	const sortParam = url.searchParams.get('sort')?.trim();
	const skipParam = url.searchParams.get('skip')?.trim();
	const takeParam = url.searchParams.get('take')?.trim();

	const tagSlugs = tagsParam
		? tagsParam.split(',').map((t) => t.trim()).filter(Boolean)
		: undefined;
	const sort = sortParam === 'popular' ? 'popular' : sortParam === 'recent' ? 'recent' : undefined;
	const skip = skipParam && /^\d+$/.test(skipParam) ? Number(skipParam) : undefined;
	const take = takeParam && /^\d+$/.test(takeParam) ? Number(takeParam) : undefined;

	try {
		const items = await fetchCatalogue(
			session.accessToken,
			{ search, tagSlugs, sort, skip, take },
			fetch
		);
		return json(items);
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message, body: err.body }, { status: err.status });
		}

		const message = err instanceof Error ? err.message : 'Failed to load catalogue.';
		return json({ error: message }, { status: 500 });
	}
};
