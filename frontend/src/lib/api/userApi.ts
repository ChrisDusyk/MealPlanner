import { ApiError, getApiBase, parseErrorBody } from './apiHelpers';

export interface AppUserResponse {
	id: string;
	authUserId: string;
	name: string;
	email: string | null;
	displayName: string | null;
	timezone: string | null;
	onboardingCompletedAt: string | null;
	createdAt: string;
	updatedAt: string;
}

export interface UpdateCurrentUserRequest {
	name: string;
}

export interface CompleteOnboardingRequest {
	displayName?: string | null;
	timezone?: string | null;
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

export async function completeOnboarding(
	accessToken: string,
	request: CompleteOnboardingRequest,
	fetchFn: typeof fetch = fetch
): Promise<AppUserResponse> {
	const response = await fetchFn(`${getApiBase()}/api/users/me/onboarding`, {
		method: 'PATCH',
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
