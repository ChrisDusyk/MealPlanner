import { describe, expect, it, vi, beforeEach } from 'vitest';

const getBooleanValue = vi.fn();
const setProviderAndWait = vi.fn().mockResolvedValue(undefined);
const getClient = vi.fn(() => ({ getBooleanValue }));
const FlagdProvider = vi.fn();

vi.mock('$app/environment', () => ({ building: false }));
vi.mock('@openfeature/server-sdk', () => ({
	OpenFeature: {
		setProviderAndWait: (...args: unknown[]) => setProviderAndWait(...args),
		getClient: (...args: unknown[]) => getClient(...args)
	}
}));
vi.mock('@openfeature/flagd-provider', () => ({ FlagdProvider }));

describe('getServerFlags', () => {
	beforeEach(() => {
		vi.resetModules();
		vi.clearAllMocks();
		getBooleanValue.mockResolvedValue(true);
		delete process.env.FLAGD_HOST;
		delete process.env.FLAGD_PORT;
		delete process.env.ConnectionStrings__flagd;
		delete process.env.services__flagd__http__0;
	});

	it('resolves the demo banner flag and forwards the evaluation context', async () => {
		const { getServerFlags, DEMO_BANNER_FLAG } = await import('./featureFlags');
		const context = { targetingKey: 'user-1' };

		const flags = await getServerFlags(context);

		expect(flags).toEqual({ demoBanner: true });
		expect(getBooleanValue).toHaveBeenCalledWith(DEMO_BANNER_FLAG, false, context);
	});

	it('configures the flagd RPC provider from FLAGD_HOST/FLAGD_PORT', async () => {
		process.env.FLAGD_HOST = 'flagd.internal';
		process.env.FLAGD_PORT = '9090';

		const { getServerFlags } = await import('./featureFlags');
		await getServerFlags();

		expect(FlagdProvider).toHaveBeenCalledWith({
			host: 'flagd.internal',
			port: 9090,
			resolverType: 'rpc'
		});
	});

	it('falls back to the flagd connection string URL when host/port are unset', async () => {
		process.env.ConnectionStrings__flagd = 'http://flagd-host:8113';

		const { getServerFlags } = await import('./featureFlags');
		await getServerFlags();

		expect(FlagdProvider).toHaveBeenCalledWith({
			host: 'flagd-host',
			port: 8113,
			resolverType: 'rpc'
		});
	});

	it('initialises the provider only once across calls', async () => {
		const { getServerFlags } = await import('./featureFlags');

		await getServerFlags();
		await getServerFlags();

		expect(setProviderAndWait).toHaveBeenCalledTimes(1);
	});
});
