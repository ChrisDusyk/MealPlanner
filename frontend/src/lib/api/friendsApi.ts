import { ApiError, getApiBase, parseErrorBody } from './apiHelpers';

export interface FriendSummary {
	userId: string;
	name: string;
	email: string | null;
}

export interface FriendRequestSummary {
	requestId: string;
	userId: string;
	name: string;
	email: string | null;
	createdAt: string;
}

export interface SendFriendRequestResponse {
	status: 'Pending' | 'Accepted' | string;
}

export interface FriendActionResponse {
	success: boolean;
}

export async function getFriends(
	accessToken: string,
	fetchFn: typeof fetch = fetch
): Promise<FriendSummary[]> {
	const response = await fetchFn(`${getApiBase()}/api/users/friends`, {
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

export async function getIncomingFriendRequests(
	accessToken: string,
	fetchFn: typeof fetch = fetch
): Promise<FriendRequestSummary[]> {
	const response = await fetchFn(`${getApiBase()}/api/users/friends/requests/incoming`, {
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

export async function getOutgoingFriendRequests(
	accessToken: string,
	fetchFn: typeof fetch = fetch
): Promise<FriendRequestSummary[]> {
	const response = await fetchFn(`${getApiBase()}/api/users/friends/requests/outgoing`, {
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

export async function sendFriendRequestByEmail(
	accessToken: string,
	email: string,
	fetchFn: typeof fetch = fetch
): Promise<SendFriendRequestResponse> {
	const response = await fetchFn(`${getApiBase()}/api/users/friends/requests`, {
		method: 'POST',
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: JSON.stringify({ email })
	});

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

export async function acceptFriendRequest(
	accessToken: string,
	requestId: string,
	fetchFn: typeof fetch = fetch
): Promise<FriendActionResponse> {
	const response = await fetchFn(
		`${getApiBase()}/api/users/friends/requests/${encodeURIComponent(requestId)}/accept`,
		{
			method: 'POST',
			headers: {
				Authorization: `Bearer ${accessToken}`
			}
		}
	);

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

export async function rejectFriendRequest(
	accessToken: string,
	requestId: string,
	fetchFn: typeof fetch = fetch
): Promise<FriendActionResponse> {
	const response = await fetchFn(
		`${getApiBase()}/api/users/friends/requests/${encodeURIComponent(requestId)}/reject`,
		{
			method: 'POST',
			headers: {
				Authorization: `Bearer ${accessToken}`
			}
		}
	);

	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}

	return response.json();
}

export async function removeFriend(
	accessToken: string,
	friendUserId: string,
	fetchFn: typeof fetch = fetch
): Promise<FriendActionResponse> {
	const response = await fetchFn(`${getApiBase()}/api/users/friends/${encodeURIComponent(friendUserId)}`, {
		method: 'DELETE',
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
