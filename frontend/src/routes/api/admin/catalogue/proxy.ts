import { getApiBase } from '$lib/api/apiHelpers';
import type { RequestEvent } from '@sveltejs/kit';

export async function proxyAdminCatalogueRequest(
	event: RequestEvent,
	pathSuffix: string = ''
): Promise<Response> {
	const apiBase = getApiBase();
	if (!apiBase) {
		return Response.json({ message: 'API base URL is not configured.' }, { status: 500 });
	}

	const { request, fetch } = event;
	const authorization = request.headers.get('authorization');
	const contentType = request.headers.get('content-type');
	const { search } = new URL(request.url);
	const normalizedSuffix = pathSuffix ? `/${pathSuffix}` : '';
	const targetUrl = `${apiBase}/api/admin/catalogue${normalizedSuffix}${search}`;

	const method = request.method.toUpperCase();
	const includeBody = method !== 'GET' && method !== 'HEAD';

	let upstreamResponse: Response;
	try {
		upstreamResponse = await fetch(targetUrl, {
			method,
			headers: {
				...(authorization ? { authorization } : {}),
				...(contentType ? { 'content-type': contentType } : {})
			},
			body: includeBody ? await request.text() : undefined
		});
	} catch {
		return Response.json({ message: 'Unable to reach API service.' }, { status: 502 });
	}

	const responseBody = await upstreamResponse.text();
	const responseContentType = upstreamResponse.headers.get('content-type') ?? 'application/json';
	const responseLocation = upstreamResponse.headers.get('location');

	const headers = new Headers({
		'content-type': responseContentType
	});
	if (responseLocation) {
		headers.set('location', responseLocation);
	}

	return new Response(responseBody, {
		status: upstreamResponse.status,
		headers
	});
}
