export const ROLE_CLAIM = 'https://mealplanner/roles';

export const APP_ROLES = {
	user: 'user',
	admin: 'admin'
} as const;

export type AppRole = (typeof APP_ROLES)[keyof typeof APP_ROLES];

const KNOWN_ROLES = new Set<AppRole>([APP_ROLES.user, APP_ROLES.admin]);

export function normalizeRoles(values: unknown[]): AppRole[] {
	const normalized = new Set<AppRole>();

	for (const value of values) {
		if (typeof value !== 'string') {
			continue;
		}

		const role = value.trim().toLowerCase();
		if (!role) {
			continue;
		}

		if (KNOWN_ROLES.has(role as AppRole)) {
			normalized.add(role as AppRole);
		}
	}

	return Array.from(normalized);
}

export function parseRoleClaimValue(value: unknown): string[] {
	if (Array.isArray(value)) {
		return value.filter((entry): entry is string => typeof entry === 'string');
	}

	if (typeof value !== 'string') {
		return [];
	}

	const trimmed = value.trim();
	if (!trimmed) {
		return [];
	}

	if (trimmed.startsWith('[') || trimmed.startsWith('"')) {
		try {
			const parsed = JSON.parse(trimmed) as unknown;
			if (Array.isArray(parsed)) {
				return parsed.filter((entry): entry is string => typeof entry === 'string');
			}
			if (typeof parsed === 'string') {
				return [parsed];
			}
		} catch {
			// Fall through to plain string parsing.
		}
	}

	if (trimmed.includes(',')) {
		return trimmed
			.split(',')
			.map((entry) => entry.trim())
			.filter((entry) => entry.length > 0);
	}

	return [trimmed];
}

function parseJwtPayload(accessToken: string): Record<string, unknown> | null {
	const parts = accessToken.split('.');
	if (parts.length < 2) {
		return null;
	}

	try {
		const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
		const padded = payload + '='.repeat((4 - (payload.length % 4)) % 4);
		const decoded = Buffer.from(padded, 'base64').toString('utf8');
		return JSON.parse(decoded) as Record<string, unknown>;
	} catch {
		return null;
	}
}

export function extractRolesFromAccessToken(accessToken: string): AppRole[] {
	if (!accessToken) {
		return [];
	}

	const payload = parseJwtPayload(accessToken);
	if (!payload) {
		return [];
	}

	return normalizeRoles(parseRoleClaimValue(payload[ROLE_CLAIM]));
}

export function extractRolesFromTokenRecord(token: Record<string, unknown>): AppRole[] {
	const directRoles = normalizeRoles(parseRoleClaimValue(token.roles));
	if (directRoles.length > 0) {
		return directRoles;
	}

	const roleClaimRoles = normalizeRoles(parseRoleClaimValue(token[ROLE_CLAIM]));
	if (roleClaimRoles.length > 0) {
		return roleClaimRoles;
	}

	const accessToken = typeof token.accessToken === 'string' ? token.accessToken : '';
	return extractRolesFromAccessToken(accessToken);
}

export function hasRole(roleList: readonly string[] | undefined, role: AppRole): boolean {
	if (!roleList || roleList.length === 0) {
		return false;
	}

	return roleList.some((candidate) => candidate.toLowerCase() === role);
}
