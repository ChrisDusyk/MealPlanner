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
import { tryResolveConnectionString } from '../src/lib/server/auth-options';

const AUTH_SCHEMA = 'auth';

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
