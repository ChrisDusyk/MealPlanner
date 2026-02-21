import { ApiError } from './recipeApi';

// ── Types ──────────────────────────────────────────────

export interface MealSlotItem {
	recipeId: string | null;
	name: string;
}

export interface DayPlan {
	day: string;
	slots: Record<string, MealSlotItem[]>;
}

export interface MealPlanResponse {
	id: string;
	weekStart: string;
	days: DayPlan[];
	createdAt: string;
	updatedAt: string;
}

export interface UpdateDaySlotRequest {
	items: MealSlotItem[];
}

export interface CopyCategoryRequest {
	sourceDay: string;
	category: string;
	targetDays: string[];
}

export const MEAL_CATEGORIES = ['Breakfast', 'Lunch', 'Supper', 'Snacks'] as const;
export type MealCategory = (typeof MEAL_CATEGORIES)[number];

export const WEEK_DAYS = [
	'Monday',
	'Tuesday',
	'Wednesday',
	'Thursday',
	'Friday',
	'Saturday',
	'Sunday'
] as const;
export type WeekDay = (typeof WEEK_DAYS)[number];

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
 * Fetch the meal plan for a given week. If no weekStart is provided,
 * defaults to the current week on the server side.
 */
export async function fetchMealPlan(
	accessToken: string,
	weekStart?: string,
	fetchFn: typeof fetch = fetch
): Promise<MealPlanResponse> {
	const params = weekStart ? `?weekStart=${weekStart}` : '';
	const response = await fetchFn(`${getApiBase()}/api/meal-plans${params}`, {
		headers: { Authorization: `Bearer ${accessToken}` }
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

/**
 * Update all items in a specific day + category slot.
 */
export async function updateDaySlot(
	accessToken: string,
	weekStart: string,
	day: string,
	category: string,
	items: MealSlotItem[],
	fetchFn: typeof fetch = fetch
): Promise<MealPlanResponse> {
	const params = new URLSearchParams({ weekStart, day, category });
	const response = await fetchFn(`${getApiBase()}/api/meal-plans/slots?${params}`, {
		method: 'PUT',
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: JSON.stringify({ items } satisfies UpdateDaySlotRequest)
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

/**
 * Copy a meal category from a source day to multiple target days.
 */
export async function copyCategory(
	accessToken: string,
	weekStart: string,
	sourceDay: string,
	category: string,
	targetDays: string[],
	fetchFn: typeof fetch = fetch
): Promise<MealPlanResponse> {
	const params = new URLSearchParams({ weekStart });
	const response = await fetchFn(`${getApiBase()}/api/meal-plans/copy-category?${params}`, {
		method: 'POST',
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: JSON.stringify({ sourceDay, category, targetDays } satisfies CopyCategoryRequest)
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

/**
 * Remove a single item from a slot by index.
 */
export async function removeSlotItem(
	accessToken: string,
	weekStart: string,
	day: string,
	category: string,
	itemIndex: number,
	fetchFn: typeof fetch = fetch
): Promise<MealPlanResponse> {
	const params = new URLSearchParams({ weekStart, day, category });
	const response = await fetchFn(`${getApiBase()}/api/meal-plans/slots/${itemIndex}?${params}`, {
		method: 'DELETE',
		headers: { Authorization: `Bearer ${accessToken}` }
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

// ── Date Utilities ─────────────────────────────────────

/**
 * Get the Monday of the week for a given date.
 */
export function getMonday(date: Date): Date {
	const d = new Date(date);
	const day = d.getDay();
	const diff = day === 0 ? 6 : day - 1;
	d.setDate(d.getDate() - diff);
	d.setHours(0, 0, 0, 0);
	return d;
}

/**
 * Format a Date as yyyy-MM-dd for API calls.
 */
export function formatDateOnly(date: Date): string {
	return date.toISOString().slice(0, 10);
}

/**
 * Get the date for a specific day within a week starting on the given Monday.
 */
export function getDateForDay(monday: Date, dayIndex: number): Date {
	const d = new Date(monday);
	d.setDate(d.getDate() + dayIndex);
	return d;
}
