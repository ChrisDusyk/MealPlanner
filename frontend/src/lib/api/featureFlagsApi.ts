import { ApiError, getApiBase, parseErrorBody } from './apiHelpers';

export interface FeatureFlagResponse {
	key: string;
	enabled: boolean;
	definitionJson: string;
	description: string | null;
	updatedAt: string;
}

/**
 * Lists all feature flags (admin only).
 */
export async function getFeatureFlags(
	accessToken: string,
	fetchFn: typeof fetch = fetch
): Promise<FeatureFlagResponse[]> {
	const response = await fetchFn(`${getApiBase()}/api/admin/feature-flags/`, {
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

/**
 * Toggles a feature flag's enabled state (admin only).
 */
export async function setFeatureFlagEnabled(
	accessToken: string,
	key: string,
	enabled: boolean,
	fetchFn: typeof fetch = fetch
): Promise<FeatureFlagResponse> {
	const response = await fetchFn(`${getApiBase()}/api/admin/feature-flags/${encodeURIComponent(key)}`, {
		method: 'PATCH',
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: JSON.stringify({ enabled })
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}
