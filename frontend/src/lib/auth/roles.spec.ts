import { describe, expect, it } from 'vitest';
import { APP_ROLES, hasRole, normalizeRoles } from './roles';

describe('normalizeRoles', () => {
	it('lowercases and deduplicates known roles', () => {
		expect(normalizeRoles(['USER', 'user', 'Admin'])).toEqual(['user', 'admin']);
	});

	it('drops unknown roles and non-strings', () => {
		expect(normalizeRoles(['editor', '', '  ', 42, null, undefined, APP_ROLES.admin])).toEqual([
			APP_ROLES.admin
		]);
	});
});

describe('hasRole', () => {
	it('matches case-insensitively', () => {
		expect(hasRole(['Admin'], APP_ROLES.admin)).toBe(true);
		expect(hasRole(['USER'], APP_ROLES.user)).toBe(true);
	});

	it('returns false for empty/missing role lists', () => {
		expect(hasRole(undefined, APP_ROLES.user)).toBe(false);
		expect(hasRole([], APP_ROLES.user)).toBe(false);
	});

	it('returns false when the role is not present', () => {
		expect(hasRole(['user'], APP_ROLES.admin)).toBe(false);
	});
});
