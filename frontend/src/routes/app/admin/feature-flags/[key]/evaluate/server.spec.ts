import { describe, expect, it, vi, beforeEach } from 'vitest';
import { ApiError } from '$lib/api/apiHelpers';

const evaluateFeatureFlag = vi.fn();

vi.mock('$lib/api/featureFlagsApi', async () => {
	const helpers = await import('$lib/api/apiHelpers');
	return {
		ApiError: helpers.ApiError,
		evaluateFeatureFlag: (...args: unknown[]) => evaluateFeatureFlag(...args)
	};
});

import { POST } from './+server';

type PostEvent = Parameters<typeof POST>[0];

const context = { targetingKey: 'u1', email: null, role: 'admin' };

function createEvent(
	session: Record<string, unknown> | null,
	body: unknown = context
): PostEvent {
	return {
		request: {
			json: vi.fn().mockImplementation(async () => {
				if (body === undefined) throw new SyntaxError('bad json');
				return body;
			})
		},
		params: { key: 'demo-banner' },
		locals: { auth: vi.fn().mockResolvedValue(session) },
		fetch: vi.fn()
	} as unknown as PostEvent;
}

describe('POST /app/admin/feature-flags/[key]/evaluate', () => {
	beforeEach(() => vi.clearAllMocks());

	it('returns the resolved value for an admin', async () => {
		evaluateFeatureFlag.mockResolvedValue({
			key: 'demo-banner',
			valueType: 'boolean',
			valueJson: 'true'
		});

		const response = await POST(createEvent({ user: { id: 'u1' }, roles: ['admin'], accessToken: 'token' }));

		expect(response.status).toBe(200);
		await expect(response.json()).resolves.toMatchObject({ valueJson: 'true' });
		expect(evaluateFeatureFlag).toHaveBeenCalledWith(
			'token',
			'demo-banner',
			context,
			expect.anything()
		);
	});

	it('rejects a non-admin', async () => {
		await expect(
			POST(createEvent({ user: { id: 'u1' }, roles: ['user'], accessToken: 'token' }))
		).rejects.toMatchObject({ status: 403 });
		expect(evaluateFeatureFlag).not.toHaveBeenCalled();
	});

	it('surfaces an unreachable flagd with its status', async () => {
		evaluateFeatureFlag.mockRejectedValue(new ApiError(502, 'flagd unreachable'));

		const response = await POST(createEvent({ user: { id: 'u1' }, roles: ['admin'], accessToken: 'token' }));

		expect(response.status).toBe(502);
		await expect(response.json()).resolves.toMatchObject({ error: 'flagd unreachable' });
	});
});
