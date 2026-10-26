/**
 * Test-only stand-in for SvelteKit's `$app/env/public` virtual module.
 *
 * In Vitest browser mode the SvelteKit runtime that normally provides the
 * module's values is absent, so importing the real virtual module throws at
 * import time. The browser test project aliases `$app/env/public` to this
 * file (see vite.config.ts).
 */
export const PUBLIC_API_URL: string | undefined = undefined;
