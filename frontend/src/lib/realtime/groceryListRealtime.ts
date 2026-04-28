import {
	HubConnectionBuilder,
	HubConnectionState,
	LogLevel,
	type HubConnection
} from '@microsoft/signalr';
import { getHubUrl } from '$lib/realtime/hubUrl';
import type { GroceryListResponse } from '$lib/api/groceryListApi';

export interface GroceryListUpdatedEvent {
	eventType: string;
	ownerUserId: string;
	weekStart: string;
	groceryList: GroceryListResponse;
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

export class GroceryListRealtimeClient {
	private connection: HubConnection | null = null;

	constructor(private readonly fetchFn: typeof fetch = fetch) {}

	async start(onListUpdated: (event: GroceryListUpdatedEvent) => void): Promise<void> {
		if (
			this.connection &&
			(this.connection.state === HubConnectionState.Connected ||
				this.connection.state === HubConnectionState.Connecting ||
				this.connection.state === HubConnectionState.Reconnecting)
		) {
			return;
		}

		const connection = new HubConnectionBuilder()
			.withUrl(getHubUrl('/hubs/grocery-lists'), {
				accessTokenFactory: () => getRealtimeAccessToken(this.fetchFn)
			})
			.withAutomaticReconnect()
			.configureLogging(LogLevel.Warning)
			.build();

		connection.on('groceryListUpdated', (event: GroceryListUpdatedEvent) => {
			onListUpdated(event);
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
