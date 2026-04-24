/**
 * Framework-agnostic Better Auth configuration.
 *
 * Split out from `./auth.ts` so the migration script (`scripts/migrate-auth.ts`)
 * can import these options without pulling in SvelteKit's `$app/server` helpers.
 */

import type { BetterAuthOptions } from 'better-auth';
import { admin, jwt } from 'better-auth/plugins';
import { Pool } from 'pg';

const AUTH_SCHEMA = 'auth';
const DEFAULT_ISSUER = 'http://localhost:3000';
const DEFAULT_AUDIENCE = 'mealplanner-api';

export function resolveConnectionString(): string {
	const explicit = process.env.DATABASE_URL?.trim();
	if (explicit) return explicit;

	const aspire = process.env.ConnectionStrings__mealplannerDb?.trim();
	if (aspire) return toPostgresUrl(aspire);

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

export function buildAuthPool(): Pool {
	const url = new URL(resolveConnectionString());

	// Pin Better Auth to the dedicated `auth` schema without affecting the
	// .NET API's use of the default `public` schema.
	const existingOptions = url.searchParams.get('options') ?? '';
	if (!existingOptions.includes('search_path')) {
		const searchPathOption = `-c search_path=${AUTH_SCHEMA}`;
		url.searchParams.set(
			'options',
			existingOptions ? `${existingOptions} ${searchPathOption}` : searchPathOption
		);
	}

	return new Pool({ connectionString: url.toString() });
}

/**
 * Build the base Better Auth configuration shared by the runtime auth instance
 * and the migration script. Framework-specific plugins (e.g. `sveltekitCookies`)
 * are layered on top in `./auth.ts`.
 */
export function createBaseAuthOptions(): BetterAuthOptions {
	const issuer = process.env.BETTER_AUTH_URL?.trim() || DEFAULT_ISSUER;
	const audience = process.env.BETTER_AUTH_JWT_AUDIENCE?.trim() || DEFAULT_AUDIENCE;

	return {
		baseURL: process.env.BETTER_AUTH_URL,
		secret: process.env.BETTER_AUTH_SECRET,
		database: buildAuthPool(),
		emailAndPassword: {
			enabled: true,
			// Email verification is disabled in development; flip to true before deploying.
			requireEmailVerification: false
		},
		socialProviders: {
			google: {
				clientId: process.env.GOOGLE_CLIENT_ID ?? '',
				clientSecret: process.env.GOOGLE_CLIENT_SECRET ?? ''
			}
		},
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
			})
		]
	};
}
