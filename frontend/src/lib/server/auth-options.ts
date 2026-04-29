/**
 * Framework-agnostic Better Auth configuration.
 *
 * Split out from `./auth.ts` so the migration script (`scripts/migrate-auth.ts`)
 * can construct a Better Auth instance without pulling in SvelteKit's
 * `$app/server` helpers.
 */

import { betterAuth, type BetterAuthPlugin } from 'better-auth';
import { admin, jwt } from 'better-auth/plugins';
import { Pool } from 'pg';

const AUTH_SCHEMA = 'auth';
const DEFAULT_ISSUER = 'http://localhost:3000';
const DEFAULT_AUDIENCE = 'mealplanner-api';
const BUILD_FALLBACK_CONNECTION = 'postgres://postgres:postgres@127.0.0.1:5432/postgres';
const BUILD_FALLBACK_SECRET = 'build-only-better-auth-secret-replace-in-runtime-env';

function parseBooleanEnv(raw: string | undefined, defaultValue = false): boolean {
	if (!raw) return defaultValue;
	const value = raw.trim().toLowerCase();
	if (['1', 'true', 'yes', 'on'].includes(value)) return true;
	if (['0', 'false', 'no', 'off'].includes(value)) return false;
	return defaultValue;
}

function resolveGoogleSocialProviders() {
	const clientId = process.env.GOOGLE_CLIENT_ID?.trim();
	const clientSecret = process.env.GOOGLE_CLIENT_SECRET?.trim();

	if (!clientId || !clientSecret) {
		return undefined;
	}

	return {
		google: {
			clientId,
			clientSecret
		}
	};
}

function resolveAuthOrigin(raw?: string): string {
	const value = raw?.trim();
	if (!value) return DEFAULT_ISSUER;

	try {
		return new URL(value).origin;
	} catch {
		return DEFAULT_ISSUER;
	}
}

export function tryResolveConnectionString(): string | null {
	const explicit = process.env.DATABASE_URL?.trim();
	if (explicit) return explicit;

	const aspire = process.env.ConnectionStrings__mealplannerDb?.trim();
	if (aspire) return toPostgresUrl(aspire);

	return null;
}

export function resolveConnectionString(options: { allowMissing?: boolean } = {}): string {
	const resolved = tryResolveConnectionString();
	if (resolved) return resolved;

	if (options.allowMissing) {
		return BUILD_FALLBACK_CONNECTION;
	}

	throw new Error(
		'Better Auth could not resolve a Postgres connection string. Set DATABASE_URL or ConnectionStrings__mealplannerDb.'
	);
}

/**
 * Aspire injects Npgsql-style key=value connection strings
 * (e.g. `Host=localhost;Port=5432;Database=db;Username=u;Password=p`).
 * Convert to a libpq URL that `pg` understands.
 */
function toPostgresUrl(connection: string): string {
	if (/^postgres(ql)?:\/\//i.test(connection)) return connection;

	const parts = connection
		.split(';')
		.map((entry) => entry.trim())
		.filter(Boolean);

	const lookup = new Map<string, string>();
	for (const entry of parts) {
		const idx = entry.indexOf('=');
		if (idx === -1) continue;
		lookup.set(entry.slice(0, idx).trim().toLowerCase(), entry.slice(idx + 1).trim());
	}

	const host = lookup.get('host') ?? lookup.get('server') ?? 'localhost';
	const port = lookup.get('port') ?? '5432';
	const database = lookup.get('database') ?? lookup.get('db') ?? 'postgres';
	const user = lookup.get('username') ?? lookup.get('user id') ?? lookup.get('user') ?? 'postgres';
	const password = lookup.get('password') ?? '';

	const auth = password
		? `${encodeURIComponent(user)}:${encodeURIComponent(password)}`
		: encodeURIComponent(user);
	return `postgres://${auth}@${host}:${port}/${encodeURIComponent(database)}`;
}

export function buildAuthPool(allowMissingConnectionString = false): Pool {
	const url = new URL(
		resolveConnectionString({ allowMissing: allowMissingConnectionString })
	);

	// Force Better Auth queries to resolve `auth."user"` style tables first.
	// Some managed Postgres URLs include an existing `search_path` in `options`;
	// appending ours last ensures `auth` wins for this pool.
	const existingOptions = url.searchParams.get('options') ?? '';
	const searchPathOption = `-c search_path=${AUTH_SCHEMA},public`;
	url.searchParams.set(
		'options',
		existingOptions ? `${existingOptions} ${searchPathOption}` : searchPathOption
	);

	return new Pool({ connectionString: url.toString() });
}

/**
 * Build a Better Auth instance. Callers may supply additional plugins
 * (e.g. the SvelteKit `sveltekitCookies` plugin) that depend on their host
 * framework without affecting the shared configuration shape used by the
 * migration script.
 *
 * Returning `betterAuth(...)` directly (rather than an options bag) preserves
 * full TypeScript inference of the resulting `auth.api` surface.
 */
export function createAuth(
	extraPlugins: BetterAuthPlugin[] = [],
	options: { allowMissingConnectionString?: boolean } = {}
) {
	const authOrigin = resolveAuthOrigin(process.env.BETTER_AUTH_URL);
	const issuer = authOrigin;
	const audience = process.env.BETTER_AUTH_JWT_AUDIENCE?.trim() || DEFAULT_AUDIENCE;
	const runtimeSecret = process.env.BETTER_AUTH_SECRET?.trim();
	const requireEmailVerification = parseBooleanEnv(
		process.env.BETTER_AUTH_REQUIRE_EMAIL_VERIFICATION,
		false
	);
	const socialProviders = resolveGoogleSocialProviders();
	const secret =
		runtimeSecret && runtimeSecret.length > 0
			? runtimeSecret
			: options.allowMissingConnectionString
				? BUILD_FALLBACK_SECRET
				: undefined;

	return betterAuth({
		baseURL: authOrigin,
		secret,
		database: buildAuthPool(options.allowMissingConnectionString ?? false),
		emailAndPassword: {
			enabled: true,
			// Controlled by BETTER_AUTH_REQUIRE_EMAIL_VERIFICATION (default false).
			requireEmailVerification
		},
		socialProviders,
		user: {
			additionalFields: {
				displayName: { type: 'string', required: false, input: true },
				timezone: { type: 'string', required: false, input: true },
				onboardingCompletedAt: { type: 'date', required: false, input: false }
			}
		},
		plugins: [
			admin(),
			jwt({
				jwks: {
					// RS256 keeps wide compatibility with Microsoft.IdentityModel.Tokens
					// (the .NET AddJwtBearer middleware).
					keyPairConfig: { alg: 'RS256', modulusLength: 2048 }
				},
				jwt: {
					issuer,
					audience,
					expirationTime: '15m',
					definePayload: ({ user }) => {
						const typed = user as typeof user & { role?: string };
						const role =
							typeof typed.role === 'string' && typed.role.length > 0 ? typed.role : 'user';
						return {
							sub: user.id,
							email: user.email,
							name: user.name,
							role,
							// Keep the existing API claim contract working unchanged.
							'https://mealplanner/roles': [role]
						};
					}
				}
			}),
			// Framework-specific plugins (e.g. `sveltekitCookies`) are appended last
			// per the Better Auth plugin ordering guidance.
			...extraPlugins
		]
	});
}
