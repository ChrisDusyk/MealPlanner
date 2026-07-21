import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getHubUrl, getHubUrlCandidates } from '$lib/realtime/hubUrl';

const mockPublicEnv = vi.hoisted((): Record<string, string | undefined> => ({}));

vi.mock('$env/dynamic/public', () => ({ env: mockPublicEnv }));

const envKeys = ['API_PUBLIC_URL', 'PUBLIC_API_URL', 'API_INTERNAL_URL', 'API_BASE_URL'] as const;
let savedEnv: Record<string, string | undefined>;

beforeEach(() => {
	savedEnv = {};
	for (const key of envKeys) {
		savedEnv[key] = process.env[key];
		delete process.env[key];
	}
	delete mockPublicEnv.PUBLIC_API_URL;
});

afterEach(() => {
	for (const key of envKeys) {
		if (savedEnv[key] === undefined) {
			delete process.env[key];
		} else {
			process.env[key] = savedEnv[key];
		}
	}
});

describe('getHubUrl', () => {
	it('uses the runtime PUBLIC_API_URL when configured', () => {
		mockPublicEnv.PUBLIC_API_URL = 'https://api.example.com/';

		expect(getHubUrl('/hubs/meal-plans')).toBe('https://api.example.com/hubs/meal-plans');
	});

	it('prefers the runtime PUBLIC_API_URL over server-side env vars', () => {
		mockPublicEnv.PUBLIC_API_URL = 'https://api.example.com';
		process.env.API_PUBLIC_URL = 'https://other.example.com';

		expect(getHubUrl('/hubs/grocery-lists')).toBe('https://api.example.com/hubs/grocery-lists');
	});

	it('falls back to server-side env vars when no runtime public URL is set', () => {
		process.env.API_PUBLIC_URL = 'https://server.example.com';

		expect(getHubUrl('/hubs/meal-plans')).toBe('https://server.example.com/hubs/meal-plans');
	});

	it('returns a relative path when no base URL is configured', () => {
		expect(getHubUrl('/hubs/meal-plans')).toBe('/hubs/meal-plans');
	});
});

describe('getHubUrlCandidates', () => {
	it('returns only the primary URL when the /api prefix fallback is disabled', () => {
		mockPublicEnv.PUBLIC_API_URL = 'https://api.example.com';

		expect(getHubUrlCandidates('/hubs/meal-plans')).toEqual([
			'https://api.example.com/hubs/meal-plans'
		]);
	});
});
