import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '$lib/api/recipeApi';
import { actions } from './+page.server';
import { updateCurrentUser } from '$lib/api/userApi';

vi.mock('$lib/api/userApi', () => ({
	updateCurrentUser: vi.fn()
}));

type AccountActionEvent = Parameters<NonNullable<typeof actions.default>>[0];

function createEvent({
	accessToken,
	name
}: {
	accessToken: string | null;
	name: string;
}): AccountActionEvent {
	const formData = new FormData();
	formData.set('name', name);

	return {
		locals: {
			auth: vi.fn().mockResolvedValue(accessToken ? { accessToken } : null)
		},
		fetch: vi.fn(),
		request: new Request('http://localhost/app/account', {
			method: 'POST',
			body: formData
		})
	} as unknown as AccountActionEvent;
}

describe('POST /app/account action', () => {
	beforeEach(() => {
		vi.clearAllMocks();
	});

	it('returns 401 when session has no access token', async () => {
		const event = createEvent({ accessToken: null, name: 'Pat' });
		const result = await actions.default(event);

		expect(result).toMatchObject({
			status: 401,
			data: {
				error: 'Unauthorized'
			}
		});
	});

	it('returns 400 when name is blank', async () => {
		const event = createEvent({ accessToken: 'test-token', name: '   ' });
		const result = await actions.default(event);

		expect(result).toMatchObject({
			status: 400,
			data: {
				error: 'Name is required.'
			}
		});
	});

	it('updates user and returns success payload', async () => {
		vi.mocked(updateCurrentUser).mockResolvedValue({
			id: 'u1',
			auth0UserId: 'auth0|123',
			name: 'Updated Name',
			email: 'pat@example.com',
			createdAt: '2026-01-01T00:00:00Z',
			updatedAt: '2026-01-02T00:00:00Z'
		});

		const event = createEvent({ accessToken: 'test-token', name: ' Updated Name ' });
		const result = await actions.default(event);

		expect(result).toMatchObject({
			success: true,
			name: 'Updated Name',
			user: {
				id: 'u1',
				name: 'Updated Name'
			}
		});
		expect(updateCurrentUser).toHaveBeenCalledWith(
			'test-token',
			{ name: 'Updated Name' },
			event.fetch
		);
	});

	it('passes through API error status/message', async () => {
		vi.mocked(updateCurrentUser).mockRejectedValue(new ApiError(404, 'User was not found.'));
		const event = createEvent({ accessToken: 'test-token', name: 'Pat' });
		const result = await actions.default(event);

		expect(result).toMatchObject({
			status: 404,
			data: {
				error: 'User was not found.',
				name: 'Pat'
			}
		});
	});
});
