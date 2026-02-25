import { ApiError } from './recipeApi';

export interface AppUserResponse {
	id: string;
	auth0UserId: string;
	name: string;
	email: string | null;
	createdAt: string;
	updatedAt: string;
}

export interface UpdateCurrentUserRequest {
	name: string;
}

function getApiBase(): string {
	const normalizeBaseUrl = (value?: string, fallbackPort?: string): string => {
		if (!value) return '';
		const trimmed = value.trim();
		if (!trimmed) return '';
		const withProtocol = /^https?:\/\//i.test(trimmed) ? trimmed : `http://${trimmed}`;
		try {
			const parsed = new URL(withProtocol);
			if (!parsed.port && fallbackPort) parsed.port = fallbackPort;
			return parsed.toString().replace(/\/$/, '');
		} catch {
			return '';
		}
	};

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

async function parseErrorBody(response: Response): Promise<{ message: string; body?: unknown }> {
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

export async function getCurrentUser(
	accessToken: string,
	fetchFn: typeof fetch = fetch
): Promise<AppUserResponse> {
	const response = await fetchFn(`${getApiBase()}/api/users/me`, {
		headers: {
			Authorization: `Bearer ${accessToken}`
		}
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

export async function updateCurrentUser(
	accessToken: string,
	request: UpdateCurrentUserRequest,
	fetchFn: typeof fetch = fetch
): Promise<AppUserResponse> {
	const response = await fetchFn(`${getApiBase()}/api/users/me`, {
		method: 'PUT',
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: JSON.stringify(request)
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}
