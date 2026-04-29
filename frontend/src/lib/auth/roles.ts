export const APP_ROLES = {
	user: 'user',
	admin: 'admin'
} as const;

export type AppRole = (typeof APP_ROLES)[keyof typeof APP_ROLES];

const KNOWN_ROLES = new Set<AppRole>([APP_ROLES.user, APP_ROLES.admin]);

export function normalizeRoles(values: unknown[]): AppRole[] {
	const normalized = new Set<AppRole>();

	for (const value of values) {
		if (typeof value !== 'string') continue;

		const role = value.trim().toLowerCase();
		if (!role) continue;

		if (KNOWN_ROLES.has(role as AppRole)) {
			normalized.add(role as AppRole);
		}
	}

	return Array.from(normalized);
}

export function hasRole(roleList: readonly string[] | undefined, role: AppRole): boolean {
	if (!roleList || roleList.length === 0) {
		return false;
	}

	return roleList.some((candidate) => candidate.toLowerCase() === role);
}
