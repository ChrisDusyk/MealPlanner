import { json, error } from '@sveltejs/kit';
import {
	acceptFamilyInvitation,
	cancelFamilyInvitation,
	declineFamilyInvitation,
	deleteFamily,
	getIncomingFamilyInvitations,
	getMyFamily,
	inviteToFamily,
	leaveFamily,
	removeFamilyMember,
	renameFamily,
	transferFamilyOwnership
} from '$lib/api/familyApi';
import { ApiError } from '$lib/api/recipeApi';
import type { RequestHandler } from './$types';

export const GET: RequestHandler = async ({ locals, fetch }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	try {
		const [family, incoming] = await Promise.all([
			getMyFamily(session.accessToken, fetch),
			getIncomingFamilyInvitations(session.accessToken, fetch)
		]);

		return json({ family, incoming });
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message }, { status: err.status });
		}
		const message = err instanceof Error ? err.message : 'Failed to load family.';
		return json({ error: message }, { status: 500 });
	}
};

interface FamilyActionBody {
	action?:
		| 'rename'
		| 'invite'
		| 'cancel-invitation'
		| 'accept-invitation'
		| 'decline-invitation'
		| 'remove-member'
		| 'leave'
		| 'transfer-ownership'
		| 'delete';
	name?: string;
	email?: string;
	invitationId?: string;
	memberUserId?: string;
	newOwnerUserId?: string;
}

export const POST: RequestHandler = async ({ request, locals, fetch }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	let body: FamilyActionBody;
	try {
		body = (await request.json()) as FamilyActionBody;
	} catch {
		return json({ error: 'Invalid JSON payload.' }, { status: 400 });
	}

	if (!body.action) {
		return json({ error: 'action is required' }, { status: 400 });
	}

	const accessToken = session.accessToken;

	try {
		switch (body.action) {
			case 'rename': {
				const name = body.name?.trim() ?? '';
				if (!name) return json({ error: 'name is required' }, { status: 400 });
				return json(await renameFamily(accessToken, name, fetch));
			}
			case 'invite': {
				const email = body.email?.trim() ?? '';
				if (!email) return json({ error: 'email is required' }, { status: 400 });
				return json(await inviteToFamily(accessToken, email, fetch));
			}
			case 'cancel-invitation': {
				const invitationId = body.invitationId?.trim() ?? '';
				if (!invitationId) return json({ error: 'invitationId is required' }, { status: 400 });
				await cancelFamilyInvitation(accessToken, invitationId, fetch);
				return json({ success: true });
			}
			case 'accept-invitation': {
				const invitationId = body.invitationId?.trim() ?? '';
				if (!invitationId) return json({ error: 'invitationId is required' }, { status: 400 });
				return json(await acceptFamilyInvitation(accessToken, invitationId, fetch));
			}
			case 'decline-invitation': {
				const invitationId = body.invitationId?.trim() ?? '';
				if (!invitationId) return json({ error: 'invitationId is required' }, { status: 400 });
				await declineFamilyInvitation(accessToken, invitationId, fetch);
				return json({ success: true });
			}
			case 'remove-member': {
				const memberUserId = body.memberUserId?.trim() ?? '';
				if (!memberUserId) return json({ error: 'memberUserId is required' }, { status: 400 });
				return json(await removeFamilyMember(accessToken, memberUserId, fetch));
			}
			case 'leave':
				return json(await leaveFamily(accessToken, fetch));
			case 'transfer-ownership': {
				const newOwnerUserId = body.newOwnerUserId?.trim() ?? '';
				if (!newOwnerUserId) return json({ error: 'newOwnerUserId is required' }, { status: 400 });
				return json(await transferFamilyOwnership(accessToken, newOwnerUserId, fetch));
			}
			case 'delete':
				return json(await deleteFamily(accessToken, fetch));
			default:
				return json({ error: `Unknown action: ${body.action as string}` }, { status: 400 });
		}
	} catch (err) {
		if (err instanceof ApiError) {
			return json({ error: err.message }, { status: err.status });
		}
		const message = err instanceof Error ? err.message : 'Family action failed.';
		return json({ error: message }, { status: 500 });
	}
};
