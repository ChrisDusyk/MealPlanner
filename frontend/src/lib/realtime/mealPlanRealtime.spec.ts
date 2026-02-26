import { describe, expect, it, vi } from 'vitest';

let latestConnection: {
	state: number;
	on: (methodName: string, callback: (event: unknown) => void) => void;
	start: () => Promise<void>;
	stop: () => Promise<void>;
	emit: (methodName: string, payload: unknown) => void;
	calls: {
		start: number;
		stop: number;
		accessTokenFactory: number;
	};
} | null = null;

vi.mock('@microsoft/signalr', () => {
	const HubConnectionState: Record<string, number> = {
		Disconnected: 0,
		Connecting: 1,
		Connected: 2,
		Reconnecting: 4
	};

	class HubConnectionBuilder {
		private accessTokenFactory: (() => string | Promise<string>) | null = null;
		withUrl(_url: string, options?: { accessTokenFactory?: () => string | Promise<string> }) {
			this.accessTokenFactory = options?.accessTokenFactory ?? null;
			return this;
		}

		withAutomaticReconnect() {
			return this;
		}

		configureLogging() {
			return this;
		}

		build() {
			const handlers = new Map<string, (event: unknown) => void>();
			const connection: {
				state: number;
				on: (methodName: string, callback: (event: unknown) => void) => void;
				start: () => Promise<void>;
				stop: () => Promise<void>;
				emit: (methodName: string, payload: unknown) => void;
				calls: {
					start: number;
					stop: number;
					accessTokenFactory: number;
				};
			} = {
				state: HubConnectionState.Disconnected,
				calls: {
					start: 0,
					stop: 0,
					accessTokenFactory: 0
				},
				on(methodName: string, callback: (event: unknown) => void) {
					handlers.set(methodName, callback);
				},
				emit(methodName: string, payload: unknown) {
					const handler = handlers.get(methodName);
					if (handler) handler(payload);
				},
				async start() {
					connection.calls.start += 1;
					connection.state = HubConnectionState.Connecting;
					if (typeof connectionAccessTokenFactory === 'function') {
						connection.calls.accessTokenFactory += 1;
						await connectionAccessTokenFactory();
					}
					connection.state = HubConnectionState.Connected;
				},
				async stop() {
					connection.calls.stop += 1;
					connection.state = HubConnectionState.Disconnected;
				}
			};

			const connectionAccessTokenFactory = this.accessTokenFactory;
			latestConnection = connection;
			return connection;
		}
	}

	const LogLevel = {
		Warning: 2
	} as const;

	return { HubConnectionBuilder, HubConnectionState, LogLevel };
});

import { MealPlanRealtimeClient } from './mealPlanRealtime';

describe('MealPlanRealtimeClient', () => {
	it('starts connection and registers mealPlanUpdated handler', async () => {
		const fetchFn = vi
			.fn()
			.mockResolvedValue({ ok: true, json: vi.fn().mockResolvedValue({ accessToken: 'token' }) });
		const onMealPlanUpdated = vi.fn();

		const client = new MealPlanRealtimeClient(fetchFn);
		await client.start(onMealPlanUpdated);

		expect(latestConnection).not.toBeNull();
		expect(latestConnection!.calls.start).toBe(1);
		expect(latestConnection!.calls.accessTokenFactory).toBe(1);

		const event = {
			eventType: 'DaySlotUpdated',
			ownerUserId: 'owner-1',
			weekStart: '2026-02-23',
			mealPlan: {
				id: 'plan-1',
				weekStart: '2026-02-23',
				days: [],
				createdAt: '2026-02-23T00:00:00Z',
				updatedAt: '2026-02-25T00:00:00Z'
			},
			changedByUserId: 'guest-1',
			occurredAt: '2026-02-25T00:00:00Z'
		};

		latestConnection!.emit('mealPlanUpdated', event);
		expect(onMealPlanUpdated).toHaveBeenCalledWith(event);
	});

	it('does not start a second connection when already connected', async () => {
		const fetchFn = vi
			.fn()
			.mockResolvedValue({ ok: true, json: vi.fn().mockResolvedValue({ accessToken: 'token' }) });
		const client = new MealPlanRealtimeClient(fetchFn);

		await client.start(() => undefined);
		await client.start(() => undefined);

		expect(latestConnection).not.toBeNull();
		expect(latestConnection!.calls.start).toBe(1);
	});

	it('stops connection and allows restart', async () => {
		const fetchFn = vi
			.fn()
			.mockResolvedValue({ ok: true, json: vi.fn().mockResolvedValue({ accessToken: 'token' }) });
		const client = new MealPlanRealtimeClient(fetchFn);

		await client.start(() => undefined);
		const firstConnection = latestConnection;

		await client.stop();
		expect(firstConnection!.calls.stop).toBe(1);

		await client.start(() => undefined);
		expect(latestConnection).not.toBe(firstConnection);
	});

	it('surfaces token retrieval errors when token endpoint fails', async () => {
		const fetchFn = vi.fn().mockResolvedValue({ ok: false, status: 401 });
		const client = new MealPlanRealtimeClient(fetchFn);

		await expect(client.start(() => undefined)).rejects.toThrow(
			'Failed to retrieve realtime token (401)'
		);
	});

	it('surfaces token retrieval errors when response has no access token', async () => {
		const fetchFn = vi.fn().mockResolvedValue({ ok: true, json: vi.fn().mockResolvedValue({}) });
		const client = new MealPlanRealtimeClient(fetchFn);

		await expect(client.start(() => undefined)).rejects.toThrow(
			'Realtime token response did not include an access token.'
		);
	});
});
