import { ApiError, getApiBase, parseErrorBody } from './apiHelpers';

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
	pantryStapleItems: GroceryListItem[];
	createdAt: string;
	updatedAt: string;
}

export interface AddCustomItemRequest {
	name: string;
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
 * Pass ownerUserId to toggle an item on a list shared with you (requires ReadWrite permission).
 */
export async function toggleGroceryListItem(
	accessToken: string,
	weekStart: string,
	itemIndex: number,
	fetchFn: typeof fetch = fetch,
	ownerUserId?: string
): Promise<GroceryListResponse> {
	const params = new URLSearchParams({ weekStart });
	if (ownerUserId) params.set('ownerUserId', ownerUserId);
	const response = await fetchFn(
		`${getApiBase()}/api/grocery-lists/items/${itemIndex}/toggle?${params}`,
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
	fetchFn: typeof fetch = fetch,
	ownerUserId?: string
): Promise<GroceryListResponse> {
	const params = new URLSearchParams({ weekStart });
	if (ownerUserId) params.set('ownerUserId', ownerUserId);

	const response = await fetchFn(`${getApiBase()}/api/grocery-lists/items?${params}`, {
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

/**
 * Promote a pantry staple item from the review section into the main grocery list.
 */
export async function promotePantryStapleItem(
	accessToken: string,
	weekStart: string,
	itemIndex: number,
	fetchFn: typeof fetch = fetch
): Promise<GroceryListResponse> {
	const response = await fetchFn(
		`${getApiBase()}/api/grocery-lists/pantry-staples/${itemIndex}/promote?weekStart=${weekStart}`,
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
