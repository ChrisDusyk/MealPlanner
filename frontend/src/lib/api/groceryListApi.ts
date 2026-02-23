import { ApiError } from './recipeApi';

// ── Types ──────────────────────────────────────────────

export interface GroceryListItem {
	name: string;
	quantity: number;
	unit: string;
	isChecked: boolean;
	sourceRecipeNames: string[];
}

export interface GroceryListResponse {
	id: string;
	weekStart: string;
	items: GroceryListItem[];
	createdAt: string;
	updatedAt: string;
}

export interface AddCustomItemRequest {
	name: string;
}

// ── Helpers ────────────────────────────────────────────

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

// ── API Functions ──────────────────────────────────────

/**
 * Generate a grocery list from a meal plan for the given week.
 */
export async function generateGroceryList(
	accessToken: string,
	weekStart: string,
	fetchFn: typeof fetch = fetch
): Promise<GroceryListResponse> {
	const response = await fetchFn(
		`${getApiBase()}/api/grocery-lists/generate?weekStart=${weekStart}`,
		{
			method: 'POST',
			headers: { Authorization: `Bearer ${accessToken}` }
		}
	);

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

/**
 * Fetch the grocery list for a given week.
 */
export async function fetchGroceryList(
	accessToken: string,
	weekStart: string,
	fetchFn: typeof fetch = fetch
): Promise<GroceryListResponse> {
	const response = await fetchFn(`${getApiBase()}/api/grocery-lists?weekStart=${weekStart}`, {
		headers: { Authorization: `Bearer ${accessToken}` }
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

/**
 * Toggle the checked state of a grocery list item by index.
 */
export async function toggleGroceryListItem(
	accessToken: string,
	weekStart: string,
	itemIndex: number,
	fetchFn: typeof fetch = fetch
): Promise<GroceryListResponse> {
	const response = await fetchFn(
		`${getApiBase()}/api/grocery-lists/items/${itemIndex}/toggle?weekStart=${weekStart}`,
		{
			method: 'PUT',
			headers: { Authorization: `Bearer ${accessToken}` }
		}
	);

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

/**
 * Add a custom item to an existing grocery list.
 */
export async function addCustomItem(
	accessToken: string,
	weekStart: string,
	name: string,
	fetchFn: typeof fetch = fetch
): Promise<GroceryListResponse> {
	const response = await fetchFn(`${getApiBase()}/api/grocery-lists/items?weekStart=${weekStart}`, {
		method: 'POST',
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: JSON.stringify({ name } satisfies AddCustomItemRequest)
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

/**
 * Delete the grocery list for a given week.
 */
export async function deleteGroceryList(
	accessToken: string,
	weekStart: string,
	fetchFn: typeof fetch = fetch
): Promise<void> {
	const response = await fetchFn(`${getApiBase()}/api/grocery-lists?weekStart=${weekStart}`, {
		method: 'DELETE',
		headers: { Authorization: `Bearer ${accessToken}` }
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}
}
