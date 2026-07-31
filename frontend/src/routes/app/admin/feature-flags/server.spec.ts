import { describe, expect, it, vi, beforeEach } from 'vitest';
import { ApiError } from '$lib/api/apiHelpers';

const createFeatureFlag = vi.fn();

vi.mock('$lib/api/featureFlagsApi', async () => {
	const helpers = await import('$lib/api/apiHelpers');
	return {
		ApiError: helpers.ApiError,
		createFeatureFlag: (...args: unknown[]) => createFeatureFlag(...args)
	};
});

import { POST } from './+server';

type PostEvent = Parameters<typeof POST>[0];

const payload = {
	key: 'new-flag',
	enabled: true,
	valueType: 'boolean',
	disabledVariant: 'off',
	definitionJson: '{"variants":{"on":true,"off":false},"defaultVariant":"on"}',
	description: null
};

/** Sentinel that makes request.json() reject, since `undefined` would just fall back to the default. */
const INVALID_JSON = Symbol('invalid json');

function createEvent(
	session: Record<string, unknown> | null,
	body: unknown = payload
): PostEvent {
	return {
		request: {
			json: vi.fn().mockImplementation(async () => {
				if (body === INVALID_JSON) throw new SyntaxError('bad json');
				return body;
			})
		},
		locals: { auth: vi.fn().mockResolvedValue(session) },
		fetch: vi.fn()
	} as unknown as PostEvent;
}

function adminSession() {
	return { user: { id: 'u1' }, roles: ['admin'], accessToken: 'token' };
}

describe('POST /app/admin/feature-flags', () => {
	beforeEach(() => vi.clearAllMocks());

	it('creates a flag for an admin', async () => {
		createFeatureFlag.mockResolvedValue({ ...payload, updatedAt: '' });

		const response = await POST(createEvent(adminSession()));

		expect(response.status).toBe(201);
		expect(createFeatureFlag).toHaveBeenCalledWith('token', payload, expect.anything());
	});

	it('rejects a non-admin', async () => {
		const session = { user: { id: 'u1' }, roles: ['user'], accessToken: 'token' };

		await expect(POST(createEvent(session))).rejects.toMatchObject({ status: 403 });
		expect(createFeatureFlag).not.toHaveBeenCalled();
	});

	it('returns 400 for an unparseable body', async () => {
		const response = await POST(createEvent(adminSession(), INVALID_JSON));

		expect(response.status).toBe(400);
		expect(createFeatureFlag).not.toHaveBeenCalled();
	});

	it('passes the API status through', async () => {
		createFeatureFlag.mockRejectedValue(new ApiError(409, 'already exists'));

		const response = await POST(createEvent(adminSession()));

		expect(response.status).toBe(409);
		await expect(response.json()).resolves.toMatchObject({ error: 'already exists' });
	});

	it('reports an unexpected failure as a 500', async () => {
		createFeatureFlag.mockRejectedValue(new Error('network down'));

		const response = await POST(createEvent(adminSession()));

		expect(response.status).toBe(500);
		await expect(response.json()).resolves.toMatchObject({ error: 'network down' });
	});
});
