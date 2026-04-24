import { error, redirect } from '@sveltejs/kit';
import { hasRole, type AppRole } from './roles';
import type { AppSession } from './session';

export function requireAuthenticatedSession(session: AppSession | null): AppSession {
	if (session?.error === 'RefreshAccessTokenError') {
		throw redirect(303, '/?session=expired');
	}

	if (!session?.user) {
		throw redirect(303, '/');
	}

	return session;
}

export function requireRole(session: AppSession | null, role: AppRole): AppSession {
	const authenticatedSession = requireAuthenticatedSession(session);
	if (!hasRole(authenticatedSession.roles, role)) {
		throw error(403, 'Forbidden');
	}

	return authenticatedSession;
}
