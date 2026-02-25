import { ApiError, getApiBase, parseErrorBody } from './apiHelpers';
import type { MealPlanResponse } from './mealPlanApi';

// ── Types ──────────────────────────────────────────────

export interface UserSummary {
	id: string;
	name: string;
	email: string | null;
}

export interface MealPlanShareResponse {
	id: string;
	ownerUserId: string;
	sharedWithUserId: string;
	sharedWithName: string;
	sharedWithEmail: string;
	weekStart: string;
	permission: string;
	sharedAt: string;
}

export interface SharedMealPlanResponse {
	shareId: string;
	ownerUserId: string;
	ownerName: string;
	ownerEmail: string;
	permission: string;
	mealPlan: MealPlanResponse;
}

export interface ShareMealPlanRequest {
	email: string;
	weekStart: string;
	permission: string;
}

// ── User Search ────────────────────────────────────────

/**
 * Search for a user by email address.
 */
export async function searchUserByEmail(
	accessToken: string,
	email: string,
	fetchFn: typeof fetch = fetch
): Promise<UserSummary> {
	const params = new URLSearchParams({ email });
	const response = await fetchFn(`${getApiBase()}/api/users/search?${params}`, {
		headers: { Authorization: `Bearer ${accessToken}` }
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

// ── Sharing API ────────────────────────────────────────

/**
 * Share a meal plan with another user.
 */
export async function shareMealPlan(
	accessToken: string,
	request: ShareMealPlanRequest,
	fetchFn: typeof fetch = fetch
): Promise<MealPlanShareResponse> {
	const response = await fetchFn(`${getApiBase()}/api/meal-plans/shares`, {
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

/**
 * Get all shares the current user has created for a given week.
 */
export async function getMyShares(
	accessToken: string,
	weekStart: string,
	fetchFn: typeof fetch = fetch
): Promise<MealPlanShareResponse[]> {
	const params = new URLSearchParams({ weekStart });
	const response = await fetchFn(`${getApiBase()}/api/meal-plans/shares?${params}`, {
		headers: { Authorization: `Bearer ${accessToken}` }
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

/**
 * Revoke (delete) a share.
 */
export async function revokeShare(
	accessToken: string,
	shareId: string,
	fetchFn: typeof fetch = fetch
): Promise<void> {
	const response = await fetchFn(`${getApiBase()}/api/meal-plans/shares/${shareId}`, {
		method: 'DELETE',
		headers: { Authorization: `Bearer ${accessToken}` }
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}
}

/**
 * Get meal plans shared with the current user for a given week.
 */
export async function getSharedWithMe(
	accessToken: string,
	weekStart: string,
	fetchFn: typeof fetch = fetch
): Promise<SharedMealPlanResponse[]> {
	const params = new URLSearchParams({ weekStart });
	const response = await fetchFn(`${getApiBase()}/api/meal-plans/shared-with-me?${params}`, {
		headers: { Authorization: `Bearer ${accessToken}` }
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

/**
 * Dismiss a meal plan that was shared with the current user.
 */
export async function dismissShare(
	accessToken: string,
	shareId: string,
	fetchFn: typeof fetch = fetch
): Promise<void> {
	const response = await fetchFn(
		`${getApiBase()}/api/meal-plans/shared-with-me/${shareId}/dismiss`,
		{
			method: 'POST',
			headers: { Authorization: `Bearer ${accessToken}` }
		}
	);

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}
}

// ── Grocery List Sharing Types ──────────────────────────

export interface GroceryListShareResponse {
	id: string;
	ownerUserId: string;
	sharedWithUserId: string;
	sharedWithName: string;
	sharedWithEmail: string;
	weekStart: string;
	permission: string;
	sharedAt: string;
}

export interface SharedGroceryListResponse {
	shareId: string;
	ownerUserId: string;
	ownerName: string;
	ownerEmail: string;
	permission: string;
	groceryList: import('./groceryListApi').GroceryListResponse;
}

export interface ShareGroceryListRequest {
	email: string;
	weekStart: string;
	permission: string;
}

// ── Grocery List Sharing API ────────────────────────────

/**
 * Share a grocery list with another user.
 */
export async function shareGroceryList(
	accessToken: string,
	request: ShareGroceryListRequest,
	fetchFn: typeof fetch = fetch
): Promise<GroceryListShareResponse> {
	const response = await fetchFn(`${getApiBase()}/api/grocery-lists/shares`, {
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

/**
 * Get all grocery list shares the current user has created for a given week.
 */
export async function getMyGroceryListShares(
	accessToken: string,
	weekStart: string,
	fetchFn: typeof fetch = fetch
): Promise<GroceryListShareResponse[]> {
	const params = new URLSearchParams({ weekStart });
	const response = await fetchFn(`${getApiBase()}/api/grocery-lists/shares?${params}`, {
		headers: { Authorization: `Bearer ${accessToken}` }
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

/**
 * Revoke (delete) a grocery list share.
 */
export async function revokeGroceryListShare(
	accessToken: string,
	shareId: string,
	fetchFn: typeof fetch = fetch
): Promise<void> {
	const response = await fetchFn(`${getApiBase()}/api/grocery-lists/shares/${shareId}`, {
		method: 'DELETE',
		headers: { Authorization: `Bearer ${accessToken}` }
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}
}

/**
 * Get grocery lists shared with the current user for a given week.
 */
export async function getGroceryListsSharedWithMe(
	accessToken: string,
	weekStart: string,
	fetchFn: typeof fetch = fetch
): Promise<SharedGroceryListResponse[]> {
	const params = new URLSearchParams({ weekStart });
	const response = await fetchFn(`${getApiBase()}/api/grocery-lists/shared-with-me?${params}`, {
		headers: { Authorization: `Bearer ${accessToken}` }
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

/**
 * Dismiss a grocery list that was shared with the current user.
 */
export async function dismissGroceryListShare(
	accessToken: string,
	shareId: string,
	fetchFn: typeof fetch = fetch
): Promise<void> {
	const response = await fetchFn(
		`${getApiBase()}/api/grocery-lists/shared-with-me/${shareId}/dismiss`,
		{
			method: 'POST',
			headers: { Authorization: `Bearer ${accessToken}` }
		}
	);

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}
}
