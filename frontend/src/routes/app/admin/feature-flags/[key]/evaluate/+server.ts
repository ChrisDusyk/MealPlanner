import { json, error } from '@sveltejs/kit';
import {
	ApiError,
	evaluateFeatureFlag,
	type EvaluateFeatureFlagRequest
} from '$lib/api/featureFlagsApi';
import { requireRole } from '$lib/auth/guards';
import { APP_ROLES } from '$lib/auth/roles';
import type { RequestHandler } from './$types';

export const POST: RequestHandler = async ({ request, params, locals, fetch }) => {
	const session = requireRole(await locals.auth(), APP_ROLES.admin);
	if (!session.accessToken) {
		error(401, 'Unauthorized');
	}

	let body: EvaluateFeatureFlagRequest;
	try {
		body = (await request.json()) as EvaluateFeatureFlagRequest;
	} catch {
		return json({ error: 'Invalid JSON payload.' }, { status: 400 });
	}

	try {
		const evaluation = await evaluateFeatureFlag(session.accessToken, params.key, body, fetch);
		return json(evaluation, { status: 200 });
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message, body: err.body }, { status: err.status });
		}

		const message = err instanceof Error ? err.message : 'Failed to evaluate the feature flag.';
		return json({ error: message }, { status: 500 });
	}
};
