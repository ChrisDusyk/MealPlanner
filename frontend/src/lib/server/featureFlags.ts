import { building } from '$app/environment';
import { OpenFeature, type EvaluationContext } from '@openfeature/server-sdk';
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
