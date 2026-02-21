export interface Ingredient {
	name: string;
	quantity: number;
	unit: string;
}

export interface Recipe {
	id: string;
	name: string;
	description: string;
	sourceUrl?: string | null;
	ingredients: Ingredient[];
	createdAt: string;
	updatedAt: string;
}

export interface CreateRecipeRequest {
	name: string;
	description: string;
	sourceUrl?: string | null;
	ingredients: Ingredient[];
}

export interface UpdateRecipeRequest {
	name: string;
	description: string;
	sourceUrl?: string | null;
	ingredients: Ingredient[];
}

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

/**
 * Resolve the API base URL. On the server side we use the Aspire service
 * discovery env vars so requests go directly to the API rather than
 * through the Vite dev-server proxy (which only handles browser requests).
 */
function getApiBase(): string {
	const normalizeBaseUrl = (value?: string, fallbackPort?: string): string => {
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
	// Fallback for client-side / browser requests.
	// In development, Vite proxy forwards this.
	// In production (Docker), we need the full URL from env var.
	// Note: In SvelteKit, use $env/static/public for public env vars, but here we are in a .ts file
	// that might be shared. For now, we'll try to access import.meta.env if available,
	// or rely on a global config if needed.
	// But actually, `recipeApi.ts` is imported by `+page.server.ts` (server) and `+page.svelte` (client).

	// Best approach for SvelteKit: use a store or context, but for simplicity here:
	// If we are in the browser, we might need a distinct base URL if not proxied.

	// However, simpliest fix for Docker deployment without proxy:
	// If we are on the server (SSR), we successfully use logic above.
	// If we are on the client (Browser), we need to know the API URL.
	// In a typical Railway setup, API and Frontend are on different domains.

	// Let's rely on Vite's transform to inject VITE_API_URL if present,
	// or fallback to relative path (which fails without proxy).

	// We'll add a check for a global variable or import.meta.env
	// to allow injecting the URL at build time or runtime.

	// For this specific file, we can try to use `import.meta.env` which Vite handles.
	return normalizeBaseUrl(import.meta.env.VITE_API_URL as string);
}

/**
 * Parse an error response body, handling both JSON and plain text.
 */
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

export async function fetchRecipes(
	accessToken: string,
	fetchFn: typeof fetch = fetch
): Promise<Recipe[]> {
	const response = await fetchFn(`${getApiBase()}/api/recipes`, {
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

export async function createRecipe(
	accessToken: string,
	request: CreateRecipeRequest,
	fetchFn: typeof fetch = fetch
): Promise<Recipe> {
	const response = await fetchFn(`${getApiBase()}/api/recipes`, {
		method: 'POST',
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

export async function fetchRecipeById(
	accessToken: string,
	id: string,
	fetchFn: typeof fetch = fetch
): Promise<Recipe> {
	const response = await fetchFn(`${getApiBase()}/api/recipes/${encodeURIComponent(id)}`, {
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

export async function updateRecipe(
	accessToken: string,
	id: string,
	request: UpdateRecipeRequest,
	fetchFn: typeof fetch = fetch
): Promise<Recipe> {
	const response = await fetchFn(`${getApiBase()}/api/recipes/${encodeURIComponent(id)}`, {
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
