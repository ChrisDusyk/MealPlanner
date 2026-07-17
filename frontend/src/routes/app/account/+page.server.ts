import { error, fail } from '@sveltejs/kit';
import { ApiError } from '$lib/api/recipeApi';
import { getIncomingFamilyInvitations, getMyFamily } from '$lib/api/familyApi';
import { updateCurrentUser } from '$lib/api/userApi';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ locals, fetch }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		return {
			family: null,
			incomingInvitations: []
		};
	}

	try {
		const [family, incomingInvitations] = await Promise.all([
			getMyFamily(session.accessToken, fetch),
			getIncomingFamilyInvitations(session.accessToken, fetch)
		]);

		return {
			family,
			incomingInvitations
		};
	} catch (err) {
		console.error('Failed to load account family data:', err);
		error(500, 'Failed to load account family data. Please try again.');
	}
};

export const actions: Actions = {
	default: async ({ request, locals, fetch }) => {
		const session = await locals.auth();
		if (!session?.accessToken) {
			return fail(401, {
				error: 'Unauthorized',
				name: ''
			});
		}

		const formData = await request.formData();
		const rawName = formData.get('name');
		const name = typeof rawName === 'string' ? rawName.trim() : '';

		if (!name) {
			return fail(400, {
				error: 'Name is required.',
				name
			});
		}

		try {
			const user = await updateCurrentUser(session.accessToken, { name }, fetch);
			return {
				success: true,
				name: user.name,
				user
			};
		} catch (err) {
			if (err instanceof ApiError) {
				return fail(err.status, {
					error: err.message,
					name
				});
			}

			return fail(500, {
				error: 'Failed to update account profile.',
				name
			});
		}
	}
};
