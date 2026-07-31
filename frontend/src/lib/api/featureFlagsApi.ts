export { ApiError } from './apiHelpers';
import { ApiError, getApiBase, parseErrorBody } from './apiHelpers';

/** JSON kind shared by every variant of a flag. Mirrors the API's FeatureFlagValueTypes. */
export const FEATURE_FLAG_VALUE_TYPES = ['boolean', 'string', 'number', 'object'] as const;

export type FeatureFlagValueType = (typeof FEATURE_FLAG_VALUE_TYPES)[number];

export interface FeatureFlagResponse {
	key: string;
	enabled: boolean;
	valueType: FeatureFlagValueType;
	disabledVariant: string | null;
	definitionJson: string;
	description: string | null;
	updatedAt: string;
}

export interface CreateFeatureFlagRequest {
	key: string;
	enabled: boolean;
	valueType: FeatureFlagValueType;
	disabledVariant: string | null;
	definitionJson: string;
	description: string | null;
}

export type UpdateFeatureFlagRequest = Omit<CreateFeatureFlagRequest, 'key'>;

export interface EvaluateFeatureFlagRequest {
	targetingKey?: string | null;
	email?: string | null;
	role?: string | null;
}

export interface EvaluateFeatureFlagResponse {
	key: string;
	valueType: FeatureFlagValueType;
	/** The resolved value rendered as JSON, so every value type fits one field. */
	valueJson: string;
}

const base = () => `${getApiBase()}/api/admin/feature-flags`;

async function jsonRequest<T>(
	url: string,
	accessToken: string,
	method: string,
	body: unknown,
	fetchFn: typeof fetch
): Promise<T> {
	const response = await fetchFn(url, {
		method,
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: body === undefined ? undefined : JSON.stringify(body)
	});

	if (!response.ok) {
		const { message, body: errorBody } = await parseErrorBody(response);
		throw new ApiError(response.status, message, errorBody);
	}

	if (response.status === 204) {
		return undefined as T;
	}

	return response.json();
}

/**
 * Lists all feature flags (admin only).
 */
export function getFeatureFlags(
	accessToken: string,
	fetchFn: typeof fetch = fetch
): Promise<FeatureFlagResponse[]> {
	// The collection route carries a trailing slash to match the API group route.
	return jsonRequest(`${base()}/`, accessToken, 'GET', undefined, fetchFn);
}

/**
 * Loads a single feature flag (admin only).
 */
export function getFeatureFlag(
	accessToken: string,
	key: string,
	fetchFn: typeof fetch = fetch
): Promise<FeatureFlagResponse> {
	return jsonRequest(`${base()}/${encodeURIComponent(key)}`, accessToken, 'GET', undefined, fetchFn);
}

/**
 * Creates a feature flag (admin only).
 */
export function createFeatureFlag(
	accessToken: string,
	request: CreateFeatureFlagRequest,
	fetchFn: typeof fetch = fetch
): Promise<FeatureFlagResponse> {
	return jsonRequest(`${base()}/`, accessToken, 'POST', request, fetchFn);
}

/**
 * Replaces a feature flag's definition (admin only). The key is immutable.
 */
export function updateFeatureFlag(
	accessToken: string,
	key: string,
	request: UpdateFeatureFlagRequest,
	fetchFn: typeof fetch = fetch
): Promise<FeatureFlagResponse> {
	return jsonRequest(`${base()}/${encodeURIComponent(key)}`, accessToken, 'PUT', request, fetchFn);
}

/**
 * Toggles a feature flag's enabled state (admin only).
 */
export function setFeatureFlagEnabled(
	accessToken: string,
	key: string,
	enabled: boolean,
	fetchFn: typeof fetch = fetch
): Promise<FeatureFlagResponse> {
	return jsonRequest(
		`${base()}/${encodeURIComponent(key)}`,
		accessToken,
		'PATCH',
		{ enabled },
		fetchFn
	);
}

/**
 * Deletes a feature flag (admin only). Code still evaluating the key silently
 * falls back to its own default, so callers should confirm first.
 */
export function deleteFeatureFlag(
	accessToken: string,
	key: string,
	fetchFn: typeof fetch = fetch
): Promise<void> {
	return jsonRequest(
		`${base()}/${encodeURIComponent(key)}`,
		accessToken,
		'DELETE',
		undefined,
		fetchFn
	);
}

/**
 * Dry-runs a flag against a sample evaluation context (admin only). Resolves
 * through live flagd, so it reflects the last synced document rather than any
 * unsaved edits.
 */
export function evaluateFeatureFlag(
	accessToken: string,
	key: string,
	request: EvaluateFeatureFlagRequest,
	fetchFn: typeof fetch = fetch
): Promise<EvaluateFeatureFlagResponse> {
	return jsonRequest(
		`${base()}/${encodeURIComponent(key)}/evaluate`,
		accessToken,
		'POST',
		request,
		fetchFn
	);
}
