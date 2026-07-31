import { describe, expect, it, vi, beforeEach } from 'vitest';
import { ApiError } from '$lib/api/apiHelpers';

const updateFeatureFlag = vi.fn();
const deleteFeatureFlag = vi.fn();

vi.mock('$lib/api/featureFlagsApi', async () => {
	const helpers = await import('$lib/api/apiHelpers');
	return {
		ApiError: helpers.ApiError,
		updateFeatureFlag: (...args: unknown[]) => updateFeatureFlag(...args),
		deleteFeatureFlag: (...args: unknown[]) => deleteFeatureFlag(...args)
	};
});

import { PUT, DELETE } from './+server';

type PutEvent = Parameters<typeof PUT>[0];
type DeleteEvent = Parameters<typeof DELETE>[0];

const payload = {
	enabled: true,
	valueType: 'boolean',
	disabledVariant: 'off',
	definitionJson: '{"variants":{"on":true,"off":false},"defaultVariant":"on"}',
	description: null
};

function adminSession() {
	return { user: { id: 'u1' }, roles: ['admin'], accessToken: 'token' };
}

function userSession() {
	return { user: { id: 'u1' }, roles: ['user'], accessToken: 'token' };
}

/** Sentinel that makes request.json() reject, since `undefined` would just fall back to the default. */
const INVALID_JSON = Symbol('invalid json');

function createPutEvent(session: Record<string, unknown> | null, body: unknown = payload): PutEvent {
	return {
		request: {
			json: vi.fn().mockImplementation(async () => {
				if (body === INVALID_JSON) throw new SyntaxError('bad json');
				return body;
			})
		},
		params: { key: 'demo-banner' },
		locals: { auth: vi.fn().mockResolvedValue(session) },
		fetch: vi.fn()
	} as unknown as PutEvent;
}

function createDeleteEvent(session: Record<string, unknown> | null): DeleteEvent {
	return {
		params: { key: 'demo-banner' },
		locals: { auth: vi.fn().mockResolvedValue(session) },
		fetch: vi.fn()
	} as unknown as DeleteEvent;
}

describe('PUT /app/admin/feature-flags/[key]', () => {
	beforeEach(() => vi.clearAllMocks());

	it('updates a flag for an admin', async () => {
		updateFeatureFlag.mockResolvedValue({ key: 'demo-banner', ...payload, updatedAt: '' });

		const response = await PUT(createPutEvent(adminSession()));

		expect(response.status).toBe(200);
		expect(updateFeatureFlag).toHaveBeenCalledWith(
			'token',
			'demo-banner',
			payload,
			expect.anything()
		);
	});

	it('rejects a non-admin', async () => {
		await expect(PUT(createPutEvent(userSession()))).rejects.toMatchObject({ status: 403 });
		expect(updateFeatureFlag).not.toHaveBeenCalled();
	});

	it('returns 400 for an unparseable body', async () => {
		const response = await PUT(createPutEvent(adminSession(), INVALID_JSON));

		expect(response.status).toBe(400);
		expect(updateFeatureFlag).not.toHaveBeenCalled();
	});

	it('passes a validation failure through with its status', async () => {
		updateFeatureFlag.mockRejectedValue(new ApiError(400, 'default variant is unknown'));

		const response = await PUT(createPutEvent(adminSession()));

		expect(response.status).toBe(400);
		await expect(response.json()).resolves.toMatchObject({
			error: 'default variant is unknown'
		});
	});
});

describe('DELETE /app/admin/feature-flags/[key]', () => {
	beforeEach(() => vi.clearAllMocks());

	it('deletes a flag for an admin', async () => {
		deleteFeatureFlag.mockResolvedValue(undefined);

		const response = await DELETE(createDeleteEvent(adminSession()));

		expect(response.status).toBe(204);
		expect(deleteFeatureFlag).toHaveBeenCalledWith('token', 'demo-banner', expect.anything());
	});

	it('rejects a non-admin', async () => {
		await expect(DELETE(createDeleteEvent(userSession()))).rejects.toMatchObject({ status: 403 });
		expect(deleteFeatureFlag).not.toHaveBeenCalled();
	});

	it('passes a missing flag through as a 404', async () => {
		deleteFeatureFlag.mockRejectedValue(new ApiError(404, 'not found'));

		const response = await DELETE(createDeleteEvent(adminSession()));

		expect(response.status).toBe(404);
	});
});
