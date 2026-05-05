import { json, error } from '@sveltejs/kit';
import { ApiError, adminImportCatalogueFromUrl } from '$lib/api/adminCatalogueApi';
import { requireRole } from '$lib/auth/guards';
import { APP_ROLES } from '$lib/auth/roles';
import type { RequestHandler } from './$types';

interface ImportFromUrlRequest {
	sourceUrl?: string;
}

export const POST: RequestHandler = async ({ request, locals, fetch }) => {
	const session = requireRole(await locals.auth(), APP_ROLES.admin);
	if (!session.accessToken) {
		error(401, 'Unauthorized');
	}

	let body: ImportFromUrlRequest;
	try {
		body = (await request.json()) as ImportFromUrlRequest;
	} catch {
		return json({ error: 'Invalid JSON payload.' }, { status: 400 });
	}

	if (!body.sourceUrl?.trim()) {
		return json({ error: 'sourceUrl is required.' }, { status: 400 });
	}

	try {
		const imported = await adminImportCatalogueFromUrl(
			session.accessToken,
			body.sourceUrl.trim(),
			fetch
		);
		return json(imported, { status: 200 });
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message, body: err.body }, { status: err.status });
		}

		const message = err instanceof Error ? err.message : 'Failed to import recipe ingredients.';
		return json({ error: message }, { status: 500 });
	}
};
