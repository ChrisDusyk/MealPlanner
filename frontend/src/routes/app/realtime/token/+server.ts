import { error, json, type RequestEvent } from '@sveltejs/kit';

export async function GET({ locals }: RequestEvent): Promise<Response> {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	return json(
		{ accessToken: session.accessToken },
		{
			headers: {
				'Cache-Control': 'no-store'
			}
		}
	);
}
