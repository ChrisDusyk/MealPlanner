#!/usr/bin/env tsx
/**
 * Idempotent Better Auth migration runner.
 *
 * 1. Ensures the dedicated `auth` schema exists in the shared Postgres database.
 * 2. Invokes Better Auth's programmatic migrator so all configured plugin tables
 *    (user, session, account, verification, jwks, ...) exist under that schema.
 *
 * Runs before `vite dev` / the production Node entrypoint so the schema stays
 * in sync without coupling it to the .NET MigrationService.
 */

import { Pool } from 'pg';

const AUTH_SCHEMA = 'auth';

function parseConnectionStringEntries(value: string): Map<string, string> {
	const entries = new Map<string, string>();

	for (const part of value.split(';')) {
		const trimmed = part.trim();
		if (!trimmed) continue;

		const separatorIndex = trimmed.indexOf('=');
		if (separatorIndex === -1) continue;

		const key = trimmed.slice(0, separatorIndex).trim().toLowerCase();
		const entryValue = trimmed.slice(separatorIndex + 1).trim();
		if (key) {
			entries.set(key, entryValue);
		}
	}

	return entries;
}

function toPostgresUrl(value: string): string | null {
	const trimmed = value.trim();
	if (!trimmed) return null;

	if (trimmed.startsWith('postgres://') || trimmed.startsWith('postgresql://')) {
		return trimmed;
	}

	if (!trimmed.includes('=')) {
		return null;
	}

	const entries = parseConnectionStringEntries(trimmed);
	const host = entries.get('host') ?? entries.get('server');
	if (!host) {
		return null;
	}

	const port = entries.get('port');
	const database =
		entries.get('database') ??
		entries.get('initial catalog') ??
		entries.get('db');
	const username =
		entries.get('username') ??
		entries.get('user id') ??
		entries.get('userid') ??
		entries.get('user');
	const password = entries.get('password') ?? entries.get('pwd');

	const url = new URL(`postgresql://${host}`);
	if (port) {
		url.port = port;
	}
	if (database) {
		url.pathname = `/${encodeURIComponent(database)}`;
	}
	if (username) {
		url.username = encodeURIComponent(username);
	}
	if (password) {
		url.password = encodeURIComponent(password);
	}

	const sslMode = entries.get('ssl mode') ?? entries.get('sslmode');
	if (sslMode) {
		url.searchParams.set('sslmode', sslMode);
	}

	const searchPath = entries.get('search path') ?? entries.get('searchpath');
	if (searchPath) {
		url.searchParams.set('options', `-c search_path=${searchPath}`);
	}

	return url.toString();
}

function tryResolveConnectionString(): string | null {
	const databaseUrl = process.env.DATABASE_URL?.trim();
	if (databaseUrl) {
		return toPostgresUrl(databaseUrl) ?? databaseUrl;
	}

	const fallback = process.env.ConnectionStrings__mealplannerDb?.trim();
	if (fallback) {
		return toPostgresUrl(fallback) ?? fallback;
	}

	return null;
}

function logMigrationTarget(connectionString: string): void {
	try {
		const parsed = new URL(connectionString);
		const database = decodeURIComponent(parsed.pathname.replace(/^\//, '') || 'postgres');
		const options = parsed.searchParams.get('options') ?? '';
		const hasExplicitSearchPath = options.includes('search_path');

		console.info(
			`[migrate-auth] Target database host=${parsed.hostname} port=${parsed.port || '5432'} db=${database} schema=${AUTH_SCHEMA} search_path=${hasExplicitSearchPath ? 'explicit' : 'default'}`
		);
	} catch {
		console.info(
			`[migrate-auth] Target schema=${AUTH_SCHEMA}. Connection string could not be parsed for diagnostics.`
		);
	}
}

async function ensureSchema(connectionString: string): Promise<void> {
	const pool = new Pool({ connectionString });
	try {
		await pool.query(`CREATE SCHEMA IF NOT EXISTS "${AUTH_SCHEMA}"`);
	} finally {
		await pool.end();
	}
}

async function main(): Promise<void> {
	const connectionString = tryResolveConnectionString();
	if (!connectionString) {
		console.warn(
			'[migrate-auth] No DATABASE_URL / ConnectionStrings__mealplannerDb configured; skipping auth migrations.'
		);
		return;
	}

	logMigrationTarget(connectionString);

	console.info(`[migrate-auth] Ensuring "${AUTH_SCHEMA}" schema exists...`);
	await ensureSchema(connectionString);

	console.info('[migrate-auth] Running Better Auth migrations...');
	// Dynamic imports so the script only touches Better Auth after the schema exists.
	// Import the framework-neutral options module (not `auth.ts`) so this script
	// can run outside a SvelteKit request context.
	const [{ getMigrations }, { createAuth }] = await Promise.all([
		import('better-auth/db/migration'),
		import('../src/lib/server/auth-options.ts')
	]);

	const migrationAuth = createAuth();
	const { runMigrations, toBeCreated, toBeAdded } = await getMigrations(migrationAuth.options);
	if (toBeCreated.length === 0 && toBeAdded.length === 0) {
		console.info('[migrate-auth] Schema already up to date.');
		return;
	}

	await runMigrations();
	console.info(
		`[migrate-auth] Applied migrations (created=${toBeCreated.length}, altered=${toBeAdded.length}).`
	);
}

main()
	.then(() => process.exit(0))
	.catch((error) => {
		console.error('[migrate-auth] Failed to run Better Auth migrations.', error);
		process.exit(1);
	});
