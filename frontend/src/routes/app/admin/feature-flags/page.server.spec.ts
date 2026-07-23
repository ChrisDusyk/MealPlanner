import { describe, expect, it, vi, beforeEach } from 'vitest';

const getFeatureFlags = vi.fn();
const setFeatureFlagEnabled = vi.fn();

vi.mock('$lib/api/featureFlagsApi', () => ({
	getFeatureFlags: (...args: unknown[]) => getFeatureFlags(...args),
	setFeatureFlagEnabled: (...args: unknown[]) => setFeatureFlagEnabled(...args)
}));

import { load, actions } from './+page.server';

type LoadEvent = Parameters<typeof load>[0];
type ToggleEvent = Parameters<(typeof actions)['toggle']>[0];

function adminSession() {
	return { user: { id: 'u1', name: 'Pat' }, roles: ['admin'], accessToken: 'token' };
}

function createLoadEvent(session: Record<string, unknown> | null): LoadEvent {
	return {
		parent: vi.fn().mockResolvedValue({ session }),
		locals: { auth: vi.fn().mockResolvedValue(session) },
		fetch: vi.fn()
	} as unknown as LoadEvent;
}

function createToggleEvent(session: Record<string, unknown> | null, form: FormData): ToggleEvent {
	return {
		request: { formData: vi.fn().mockResolvedValue(form) },
		locals: { auth: vi.fn().mockResolvedValue(session) },
		fetch: vi.fn()
	} as unknown as ToggleEvent;
}

describe('feature flags admin load', () => {
	beforeEach(() => vi.clearAllMocks());

	it('returns flags for an admin', async () => {
		const flags = [{ key: 'demo-banner', enabled: false, definitionJson: '{}', description: null, updatedAt: '' }];
		getFeatureFlags.mockResolvedValue(flags);

		const result = await load(createLoadEvent(adminSession()));

		expect(result).toEqual({ flags });
		expect(getFeatureFlags).toHaveBeenCalledWith('token', expect.anything());
	});

	it('throws forbidden for a non-admin', async () => {
		const session = { user: { id: 'u1' }, roles: ['user'], accessToken: 'token' };
		await expect(load(createLoadEvent(session))).rejects.toMatchObject({ status: 403 });
		expect(getFeatureFlags).not.toHaveBeenCalled();
	});
});

describe('feature flags toggle action', () => {
	beforeEach(() => vi.clearAllMocks());

	it('toggles a flag for an admin', async () => {
		const updated = { key: 'demo-banner', enabled: true, definitionJson: '{}', description: null, updatedAt: '' };
		setFeatureFlagEnabled.mockResolvedValue(updated);

		const form = new FormData();
		form.set('key', 'demo-banner');
		form.set('enabled', 'true');

		const result = await actions.toggle(createToggleEvent(adminSession(), form));

		expect(setFeatureFlagEnabled).toHaveBeenCalledWith('token', 'demo-banner', true, expect.anything());
		expect(result).toEqual({ flag: updated });
	});

	it('fails validation when the key is missing', async () => {
		const form = new FormData();
		form.set('enabled', 'true');

		const result = await actions.toggle(createToggleEvent(adminSession(), form));

		expect(result).toMatchObject({ status: 400 });
		expect(setFeatureFlagEnabled).not.toHaveBeenCalled();
	});

	it('rejects a non-admin', async () => {
		const session = { user: { id: 'u1' }, roles: ['user'], accessToken: 'token' };
		const form = new FormData();
		form.set('key', 'demo-banner');
		form.set('enabled', 'true');

		await expect(actions.toggle(createToggleEvent(session, form))).rejects.toMatchObject({ status: 403 });
		expect(setFeatureFlagEnabled).not.toHaveBeenCalled();
	});
});
