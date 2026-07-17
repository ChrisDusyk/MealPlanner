import { ApiError, getApiBase, parseErrorBody } from './apiHelpers';

// ── Types ──────────────────────────────────────────────

export interface FamilyMemberResponse {
	userId: string;
	name: string;
	email: string | null;
	isOwner: boolean;
	joinedAt: string;
}

export interface FamilyInvitationResponse {
	id: string;
	familyGroupId: string;
	familyName: string;
	inviterName: string;
	inviteeName: string;
	inviteeEmail: string | null;
	createdAt: string;
}

export interface MyFamilyResponse {
	id: string;
	name: string;
	isOwner: boolean;
	members: FamilyMemberResponse[];
	pendingInvitations: FamilyInvitationResponse[];
	createdAt: string;
	updatedAt: string;
}

// ── API Functions ──────────────────────────────────────

async function handleResponse<T>(response: Response): Promise<T> {
	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}
	return response.json();
}

async function handleEmptyResponse(response: Response): Promise<void> {
	if (!response.ok) {
		const { message, body } = await parseErrorBody(response);
		throw new ApiError(response.status, message, body);
	}
}

/** Fetch the caller's family (auto-provisioned when they have none yet). */
export async function getMyFamily(
	accessToken: string,
	fetchFn: typeof fetch = fetch
): Promise<MyFamilyResponse> {
	const response = await fetchFn(`${getApiBase()}/api/families/mine`, {
		headers: { Authorization: `Bearer ${accessToken}` }
	});
	return handleResponse(response);
}

/** Rename the family (owner only). */
export async function renameFamily(
	accessToken: string,
	name: string,
	fetchFn: typeof fetch = fetch
): Promise<MyFamilyResponse> {
	const response = await fetchFn(`${getApiBase()}/api/families/mine`, {
		method: 'PUT',
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: JSON.stringify({ name })
	});
	return handleResponse(response);
}

/** Invite a registered user to the family by email (owner only). */
export async function inviteToFamily(
	accessToken: string,
	email: string,
	fetchFn: typeof fetch = fetch
): Promise<FamilyInvitationResponse> {
	const response = await fetchFn(`${getApiBase()}/api/families/mine/invitations`, {
		method: 'POST',
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: JSON.stringify({ email })
	});
	return handleResponse(response);
}

/** Cancel a pending outgoing invitation (owner only). */
export async function cancelFamilyInvitation(
	accessToken: string,
	invitationId: string,
	fetchFn: typeof fetch = fetch
): Promise<void> {
	const response = await fetchFn(
		`${getApiBase()}/api/families/mine/invitations/${encodeURIComponent(invitationId)}`,
		{
			method: 'DELETE',
			headers: { Authorization: `Bearer ${accessToken}` }
		}
	);
	return handleEmptyResponse(response);
}

/** List invitations addressed to the caller. */
export async function getIncomingFamilyInvitations(
	accessToken: string,
	fetchFn: typeof fetch = fetch
): Promise<FamilyInvitationResponse[]> {
	const response = await fetchFn(`${getApiBase()}/api/families/invitations/incoming`, {
		headers: { Authorization: `Bearer ${accessToken}` }
	});
	return handleResponse(response);
}

/** Accept an invitation, joining that family and leaving the current one. */
export async function acceptFamilyInvitation(
	accessToken: string,
	invitationId: string,
	fetchFn: typeof fetch = fetch
): Promise<MyFamilyResponse> {
	const response = await fetchFn(
		`${getApiBase()}/api/families/invitations/${encodeURIComponent(invitationId)}/accept`,
		{
			method: 'POST',
			headers: { Authorization: `Bearer ${accessToken}` }
		}
	);
	return handleResponse(response);
}

/** Decline an invitation addressed to the caller. */
export async function declineFamilyInvitation(
	accessToken: string,
	invitationId: string,
	fetchFn: typeof fetch = fetch
): Promise<void> {
	const response = await fetchFn(
		`${getApiBase()}/api/families/invitations/${encodeURIComponent(invitationId)}/decline`,
		{
			method: 'POST',
			headers: { Authorization: `Bearer ${accessToken}` }
		}
	);
	return handleEmptyResponse(response);
}

/** Remove a member from the family (owner only). */
export async function removeFamilyMember(
	accessToken: string,
	memberUserId: string,
	fetchFn: typeof fetch = fetch
): Promise<MyFamilyResponse> {
	const response = await fetchFn(
		`${getApiBase()}/api/families/mine/members/${encodeURIComponent(memberUserId)}`,
		{
			method: 'DELETE',
			headers: { Authorization: `Bearer ${accessToken}` }
		}
	);
	return handleResponse(response);
}

/** Leave the family; the caller gets a fresh personal family. */
export async function leaveFamily(
	accessToken: string,
	fetchFn: typeof fetch = fetch
): Promise<MyFamilyResponse> {
	const response = await fetchFn(`${getApiBase()}/api/families/mine/leave`, {
		method: 'POST',
		headers: { Authorization: `Bearer ${accessToken}` }
	});
	return handleResponse(response);
}

/** Transfer family ownership to another member (owner only). */
export async function transferFamilyOwnership(
	accessToken: string,
	newOwnerUserId: string,
	fetchFn: typeof fetch = fetch
): Promise<MyFamilyResponse> {
	const response = await fetchFn(`${getApiBase()}/api/families/mine/transfer-ownership`, {
		method: 'POST',
		headers: {
			'Content-Type': 'application/json',
			Authorization: `Bearer ${accessToken}`
		},
		body: JSON.stringify({ newOwnerUserId })
	});
	return handleResponse(response);
}

/** Delete the family (owner only); every member gets a fresh personal family. */
export async function deleteFamily(
	accessToken: string,
	fetchFn: typeof fetch = fetch
): Promise<MyFamilyResponse> {
	const response = await fetchFn(`${getApiBase()}/api/families/mine`, {
		method: 'DELETE',
		headers: { Authorization: `Bearer ${accessToken}` }
	});
	return handleResponse(response);
}
