import { building } from '$app/environment';
import { OpenFeature, type EvaluationContext, type JsonValue } from '@openfeature/server-sdk';
import { FlagdProvider } from '@openfeature/flagd-provider';

/**
 * Feature flags resolved server-side and passed to the browser via page data.
 * Evaluation happens only on the SvelteKit server (SSR) against flagd over gRPC
 * (RPC resolver); the browser never talks to flagd directly.
 */
export interface ResolvedFlags {
	demoBanner: boolean;
}

export const DEMO_BANNER_FLAG = 'demo-banner';

const DEFAULT_FLAGS: ResolvedFlags = {
	demoBanner: false
};

let providerReady = false;
let initPromise: Promise<void> | null = null;

/**
 * Resolves the flagd gRPC endpoint. Mirrors the server-side env convention used
 * by {@link file://./../api/apiHelpers.ts}: `FLAGD_HOST`/`FLAGD_PORT` are set by
 * the Aspire flagd reference in development and as Railway service variables in
 * production; the Aspire connection string / service-discovery URL is a fallback.
 */
function resolveFlagdEndpoint(): { host: string; port: number } {
	const host = process.env.FLAGD_HOST?.trim();
	const port = process.env.FLAGD_PORT?.trim();
	if (host && port) {
		return { host, port: Number(port) };
	}

	const url = process.env.ConnectionStrings__flagd ?? process.env.services__flagd__http__0 ?? '';
	if (url) {
		try {
			const parsed = new URL(url);
			return { host: parsed.hostname, port: Number(parsed.port) || 8013 };
		} catch {
			// fall through to the default below
		}
	}

	return { host: 'localhost', port: 8013 };
}

/**
 * Lazily installs the flagd provider on the OpenFeature global exactly once. If
 * flagd is unreachable the default (no-op) provider stays in place so
 * evaluations fall back to their code defaults, and a later request can retry.
 */
async function ensureProvider(): Promise<void> {
	if (providerReady) return;

	if (!initPromise) {
		const { host, port } = resolveFlagdEndpoint();
		initPromise = OpenFeature.setProviderAndWait(
			new FlagdProvider({ host, port, resolverType: 'rpc' })
		)
			.then(() => {
				providerReady = true;
			})
			.catch((error) => {
				console.warn('Failed to initialise the flagd provider; using flag defaults.', error);
				initPromise = null;
			});
	}

	await initPromise;
}

/**
 * Resolves all feature flags for the current request. Safe to call from server
 * load functions; returns defaults during build/prerender.
 */
export async function getServerFlags(context?: EvaluationContext): Promise<ResolvedFlags> {
	if (building) {
		return { ...DEFAULT_FLAGS };
	}

	await ensureProvider();
	const client = OpenFeature.getClient();

	const demoBanner = await client.getBooleanValue(DEMO_BANNER_FLAG, DEFAULT_FLAGS.demoBanner, context);

	return { demoBanner };
}

/**
 * Resolves a single flag of any type. {@link getServerFlags} covers the flags the
 * root layout hands to every page; this is for flags read on demand by a
 * specific route, including the string / number / object flags the admin editor
 * can author.
 *
 * The supplied default is returned when flagd is unreachable or does not know
 * the key, so callers keep working while a flag is being introduced or after one
 * is deleted.
 */
export async function getFlagValue<T extends JsonValue>(
	key: string,
	defaultValue: T,
	context?: EvaluationContext
): Promise<T> {
	if (building) {
		return defaultValue;
	}

	await ensureProvider();
	const client = OpenFeature.getClient();

	switch (typeof defaultValue) {
		case 'boolean':
			return (await client.getBooleanValue(key, defaultValue, context)) as T;
		case 'string':
			return (await client.getStringValue(key, defaultValue, context)) as T;
		case 'number':
			return (await client.getNumberValue(key, defaultValue, context)) as T;
		default:
			return (await client.getObjectValue(key, defaultValue, context)) as T;
	}
}
