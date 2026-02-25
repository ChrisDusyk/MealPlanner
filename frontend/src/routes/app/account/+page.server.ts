import { fail } from '@sveltejs/kit';
import { ApiError } from '$lib/api/recipeApi';
import { updateCurrentUser } from '$lib/api/userApi';
import type { Actions } from './$types';

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
