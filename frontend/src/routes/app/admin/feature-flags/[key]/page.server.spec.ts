import { describe, expect, it, vi, beforeEach } from 'vitest';
import { ApiError } from '$lib/api/apiHelpers';

const getFeatureFlag = vi.fn();

vi.mock('$lib/api/featureFlagsApi', () => ({
	getFeatureFlag: (...args: unknown[]) => getFeatureFlag(...args)
}));

import { load } from './+page.server';

type LoadEvent = Parameters<typeof load>[0];

function adminSession() {
	return { user: { id: 'u1', name: 'Pat' }, roles: ['admin'], accessToken: 'token' };
}

function createLoadEvent(session: Record<string, unknown> | null, key = 'demo-banner'): LoadEvent {
	return {
		parent: vi.fn().mockResolvedValue({ session }),
		params: { key },
		locals: { auth: vi.fn().mockResolvedValue(session) },
		fetch: vi.fn()
	} as unknown as LoadEvent;
}

describe('feature flag edit load', () => {
	beforeEach(() => vi.clearAllMocks());

	it('returns the flag for an admin', async () => {
		const flag = {
			key: 'demo-banner',
			enabled: false,
			valueType: 'boolean',
			disabledVariant: 'off',
			definitionJson: '{}',
			description: null,
			updatedAt: ''
		};
		getFeatureFlag.mockResolvedValue(flag);

		const result = await load(createLoadEvent(adminSession()));

		expect(result).toEqual({ flag });
		expect(getFeatureFlag).toHaveBeenCalledWith('token', 'demo-banner', expect.anything());
	});

	it('throws forbidden for a non-admin', async () => {
		const session = { user: { id: 'u1' }, roles: ['user'], accessToken: 'token' };

		await expect(load(createLoadEvent(session))).rejects.toMatchObject({ status: 403 });
		expect(getFeatureFlag).not.toHaveBeenCalled();
	});

	it('surfaces a missing flag as a 404 rather than a 500', async () => {
		getFeatureFlag.mockRejectedValue(new ApiError(404, 'not found'));

		await expect(load(createLoadEvent(adminSession(), 'ghost'))).rejects.toMatchObject({
			status: 404
		});
	});

	it('throws a server error when the API fails', async () => {
		getFeatureFlag.mockRejectedValue(new ApiError(500, 'boom'));

		await expect(load(createLoadEvent(adminSession()))).rejects.toMatchObject({ status: 500 });
	});
});
