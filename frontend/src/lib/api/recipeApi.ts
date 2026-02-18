export interface Ingredient {
	name: string;
	quantity: number;
	unit: string;
}

export interface Recipe {
	id: string;
	name: string;
	description: string;
	sourceUrl: string;
	ingredients: Ingredient[];
	createdAt: string;
	updatedAt: string;
}

export interface CreateRecipeRequest {
	name: string;
	description: string;
	sourceUrl: string;
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
	if (typeof process !== 'undefined') {
		// Must use HTTPS: the API has UseHttpsRedirection() which redirects
		// HTTP → HTTPS, and fetch drops the Authorization header on redirect.
		const url = process.env.services__api__https__0 || process.env.services__api__http__0;
		if (url) return url;
	}
	// Fallback for client-side / browser requests — use relative URL
	// which the Vite proxy will forward during dev.
	return '';
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
