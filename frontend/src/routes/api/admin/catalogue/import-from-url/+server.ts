import { getApiBase } from '$lib/api/apiHelpers';
import type { RequestHandler } from './$types';

export const POST: RequestHandler = async ({ request, fetch }) => {
	const apiBase = getApiBase();
	if (!apiBase) {
		return Response.json(
			{ message: 'API base URL is not configured.' },
			{ status: 500 }
		);
	}

	const authorization = request.headers.get('authorization');
	const requestBody = await request.text();

	let upstreamResponse: Response;
	try {
		upstreamResponse = await fetch(`${apiBase}/api/admin/catalogue/import-from-url`, {
			method: 'POST',
			headers: {
				'content-type': 'application/json',
				...(authorization ? { authorization } : {})
			},
			body: requestBody
		});
	} catch {
		return Response.json(
			{ message: 'Unable to reach API service.' },
			{ status: 502 }
		);
	}

	const responseBody = await upstreamResponse.text();
	const contentType = upstreamResponse.headers.get('content-type') ?? 'application/json';

	return new Response(responseBody, {
		status: upstreamResponse.status,
		headers: {
			'content-type': contentType
		}
	});
};
