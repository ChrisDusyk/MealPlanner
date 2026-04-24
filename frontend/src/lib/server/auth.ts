import { betterAuth } from 'better-auth';
import { admin, jwt } from 'better-auth/plugins';
import { sveltekitCookies } from 'better-auth/svelte-kit';
import { getRequestEvent } from '$app/server';
import { Pool } from 'pg';

const AUTH_SCHEMA = 'auth';
const JWT_AUDIENCE = process.env.BETTER_AUTH_JWT_AUDIENCE?.trim() || 'mealplanner-api';
const JWT_ISSUER = process.env.BETTER_AUTH_URL?.trim() || 'http://localhost:3000';

function resolveConnectionString(): string {
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
 * (e.g. "Host=localhost;Port=5432;Database=db;Username=u;Password=p").
 * Convert to a libpq URL that `pg` understands.
 */
function toPostgresUrl(connection: string): string {
	if (/^postgres(ql)?:\/\//i.test(connection)) {
		return connection;
	}

	const parts = connection
		.split(';')
		.map((entry) => entry.trim())
		.filter((entry) => entry.length > 0);

	const lookup = new Map<string, string>();
	for (const entry of parts) {
		const idx = entry.indexOf('=');
		if (idx === -1) continue;
		const key = entry.slice(0, idx).trim().toLowerCase();
		const value = entry.slice(idx + 1).trim();
		lookup.set(key, value);
	}

	const host = lookup.get('host') ?? lookup.get('server') ?? 'localhost';
	const port = lookup.get('port') ?? '5432';
	const database = lookup.get('database') ?? lookup.get('db') ?? 'postgres';
	const user = lookup.get('username') ?? lookup.get('user id') ?? lookup.get('user') ?? 'postgres';
	const password = lookup.get('password') ?? '';

	const encoded = (value: string) => encodeURIComponent(value);
	const auth = password ? `${encoded(user)}:${encoded(password)}` : encoded(user);
	return `postgres://${auth}@${host}:${port}/${encoded(database)}`;
}

function buildPool(): Pool {
	const base = resolveConnectionString();
	const url = new URL(base);

	// Pin Better Auth to the dedicated `auth` schema without affecting
	// the .NET API's use of the default `public` schema.
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

export const auth = betterAuth({
	baseURL: process.env.BETTER_AUTH_URL,
	secret: process.env.BETTER_AUTH_SECRET,
	database: buildPool(),
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
			displayName: {
				type: 'string',
				required: false,
				input: true
			},
			timezone: {
				type: 'string',
				required: false,
				input: true
			},
			onboardingCompletedAt: {
				type: 'date',
				required: false,
				input: false
			}
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
				issuer: JWT_ISSUER,
				audience: JWT_AUDIENCE,
				expirationTime: '15m',
				definePayload: ({ user }) => {
					const typed = user as typeof user & { role?: string };
					const role = typeof typed.role === 'string' && typed.role.length > 0 ? typed.role : 'user';
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
		// sveltekitCookies must be the last plugin per Better Auth docs.
		sveltekitCookies(getRequestEvent)
	]
});
