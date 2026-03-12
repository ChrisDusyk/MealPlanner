import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ url }) => {
	return {
		sessionExpired: url.searchParams.get('session') === 'expired'
	};
};
