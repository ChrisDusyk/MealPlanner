import { auth } from './auth';
import type { AppSession, AppSessionUser } from '$lib/auth/session';

/**
 * Resolve the current request into the app's session shape, or `null` if
 * the user is unauthenticated. The returned session includes a freshly
 * minted Better Auth JWT suitable for calling the .NET API.
 */
export async function resolveAppSession(request: Request): Promise<AppSession | null> {
	const sessionResult = await auth.api.getSession({ headers: request.headers });
	if (!sessionResult?.user) {
		return null;
	}

	const rawUser = sessionResult.user as Record<string, unknown>;
	const role =
		typeof rawUser.role === 'string' && (rawUser.role as string).length > 0
			? (rawUser.role as string)
			: 'user';

	let accessToken = '';
	let error: AppSession['error'];
	try {
		const tokenResult = await auth.api.getToken({ headers: request.headers });
		accessToken =
			typeof tokenResult?.token === 'string' && tokenResult.token.length > 0
				? tokenResult.token
				: '';
		if (!accessToken) {
			error = 'RefreshAccessTokenError';
		}
	} catch (err) {
		console.warn('Failed to mint Better Auth JWT for request.', err);
		error = 'RefreshAccessTokenError';
	}

	const onboardingRaw = rawUser.onboardingCompletedAt;
	let onboardingCompletedAt: string | null = null;
	if (typeof onboardingRaw === 'string') {
		onboardingCompletedAt = onboardingRaw;
	} else if (onboardingRaw instanceof Date) {
		onboardingCompletedAt = onboardingRaw.toISOString();
	}

	const user: AppSessionUser = {
		id: String(rawUser.id),
		email: typeof rawUser.email === 'string' ? rawUser.email : null,
		name: typeof rawUser.name === 'string' ? rawUser.name : null,
		image: typeof rawUser.image === 'string' ? rawUser.image : null,
		role,
		displayName: typeof rawUser.displayName === 'string' ? rawUser.displayName : null,
		timezone: typeof rawUser.timezone === 'string' ? rawUser.timezone : null,
		onboardingCompletedAt
	};

	return {
		user,
		accessToken,
		roles: [role],
		error
	};
}
