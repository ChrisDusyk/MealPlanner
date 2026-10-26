import { defineEnvVars } from '@sveltejs/kit/env';

export const variables = defineEnvVars({
	PUBLIC_API_URL: {
		public: true,
		description: 'Browser-reachable base URL of the API; read at runtime so images need no rebuild',
		schema: (value) => value
	}
});
