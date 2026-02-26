import {
	HubConnectionBuilder,
	HubConnectionState,
	LogLevel,
	type HubConnection
} from '@microsoft/signalr';
import type { MealPlanResponse } from '$lib/api/mealPlanApi';

export interface MealPlanUpdatedEvent {
	eventType: string;
	ownerUserId: string;
	weekStart: string;
	mealPlan: MealPlanResponse;
	changedByUserId: string;
	occurredAt: string;
}

async function getRealtimeAccessToken(fetchFn: typeof fetch): Promise<string> {
	const response = await fetchFn('/app/realtime/token');
	if (!response.ok) {
		throw new Error(`Failed to retrieve realtime token (${response.status}).`);
	}

	const body = (await response.json().catch(() => ({}))) as { accessToken?: string };
	const accessToken = body.accessToken?.trim() ?? '';
	if (!accessToken) {
		throw new Error('Realtime token response did not include an access token.');
	}

	return accessToken;
}

export class MealPlanRealtimeClient {
	private connection: HubConnection | null = null;

	constructor(private readonly fetchFn: typeof fetch = fetch) {}

	async start(onMealPlanUpdated: (event: MealPlanUpdatedEvent) => void): Promise<void> {
		if (
			this.connection &&
			(this.connection.state === HubConnectionState.Connected ||
				this.connection.state === HubConnectionState.Connecting ||
				this.connection.state === HubConnectionState.Reconnecting)
		) {
			return;
		}

		const connection = new HubConnectionBuilder()
			.withUrl('/hubs/meal-plans', {
				accessTokenFactory: () => getRealtimeAccessToken(this.fetchFn)
			})
			.withAutomaticReconnect()
			.configureLogging(LogLevel.Warning)
			.build();

		connection.on('mealPlanUpdated', (event: MealPlanUpdatedEvent) => {
			onMealPlanUpdated(event);
		});

		await connection.start();
		this.connection = connection;
	}

	async stop(): Promise<void> {
		if (!this.connection) return;

		const connection = this.connection;
		this.connection = null;
		await connection.stop();
	}
}
