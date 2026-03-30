import type { Session } from '@auth/sveltekit';
import { error, redirect } from '@sveltejs/kit';
import { hasRole, type AppRole } from './roles';

export function requireAuthenticatedSession(session: Session | null): Session {
	if (session?.error === 'RefreshAccessTokenError') {
		throw redirect(303, '/?session=expired');
	}

	if (!session?.user) {
		throw redirect(303, '/');
	}

	return session;
}

export function requireRole(session: Session | null, role: AppRole): Session {
	const authenticatedSession = requireAuthenticatedSession(session);
	if (!hasRole(authenticatedSession.roles, role)) {
		throw error(403, 'Forbidden');
	}

	return authenticatedSession;
}
