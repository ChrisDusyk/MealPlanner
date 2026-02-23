import { ApiError } from './recipeApi';
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
