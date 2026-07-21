/**
 * Test-only stand-in for SvelteKit's `$env/dynamic/public` virtual module.
 *
 * In Vitest browser mode the SvelteKit client runtime that normally provides
 * the module's values is absent, so importing the real virtual module throws
 * at import time. The browser test project aliases `$env/dynamic/public` to
 * this file (see vite.config.ts); tests can mutate `env` to simulate
 * configured values.
 */
export const env: Record<string, string | undefined> = {};
