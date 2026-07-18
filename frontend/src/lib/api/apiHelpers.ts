import { env as publicEnv } from '$env/dynamic/public';

/**
 * Custom error that preserves the HTTP status code from an API response.
 */
export class ApiError extends Error {
	status: number;
	body: unknown;

	constructor(status: number, message: string, body?: unknown) {
		super(message);
		this.name = 'ApiError';
		this.status = status;
		this.body = body;
	}
}

function normalizeBaseUrl(value?: string, fallbackPort?: string): string {
	if (!value) return '';

	const trimmed = value.trim();
	if (!trimmed) return '';

	const withProtocol = /^https?:\/\//i.test(trimmed) ? trimmed : `http://${trimmed}`;

	try {
		const parsed = new URL(withProtocol);
		if (!parsed.port && fallbackPort) {
			parsed.port = fallbackPort;
		}
		return parsed.toString().replace(/\/$/, '');
	} catch {
		return '';
	}
}

/**
 * Resolve the API base URL. On the server side we use the Aspire service
 * discovery env vars so requests go directly to the API rather than
 * through the Vite dev-server proxy (which only handles browser requests).
 */
export function getApiBase(): string {
	if (typeof process !== 'undefined') {
		const explicitApiUrl = normalizeBaseUrl(
			process.env.API_INTERNAL_URL || process.env.API_BASE_URL,
			process.env.API_PORT
		);
		if (explicitApiUrl) return explicitApiUrl;

		const serviceDiscoveryUrl = normalizeBaseUrl(
			process.env.services__api__https__0 || process.env.services__api__http__0
		);
		if (serviceDiscoveryUrl) return serviceDiscoveryUrl;
	}

	return normalizeBaseUrl(import.meta.env.VITE_API_URL as string);
}

/**
 * Resolve a browser-reachable API base URL for realtime hub connections.
 * Falls back to getApiBase so existing local/dev behavior continues to work.
 */
export function getPublicApiBase(): string {
	// Runtime-configurable (adapter-node serializes PUBLIC_* vars to the browser),
	// so deployments can point the browser at the API without rebuilding the image.
	const runtimePublicApiUrl = normalizeBaseUrl(publicEnv.PUBLIC_API_URL);
	if (runtimePublicApiUrl) return runtimePublicApiUrl;

	if (typeof process !== 'undefined') {
		const explicitPublicApiUrl = normalizeBaseUrl(
			process.env.API_PUBLIC_URL || process.env.PUBLIC_API_URL
		);
		if (explicitPublicApiUrl) return explicitPublicApiUrl;
	}

	const vitePublicApiUrl = normalizeBaseUrl(import.meta.env.VITE_API_PUBLIC_URL as string);
	if (vitePublicApiUrl) return vitePublicApiUrl;

	return getApiBase();
}

/**
 * Parse an error response body, handling both JSON and plain text.
 */
export async function parseErrorBody(
	response: Response
): Promise<{ message: string; body?: unknown }> {
	const contentType = response.headers.get('content-type') || '';
	if (contentType.includes('application/json')) {
		try {
			const json = await response.json();
			const message =
				typeof json === 'string'
					? json
					: json.message || json.error || json.title || JSON.stringify(json);
			return { message, body: json };
		} catch {
			return { message: `Request failed with status ${response.status}` };
		}
	}
	const text = await response.text();
	return { message: text || `Request failed with status ${response.status}` };
}
